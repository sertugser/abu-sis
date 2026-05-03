using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;
using SIS.Infrastructure.Services;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class MrvWeights
{
    public double ClassroomWeight { get; init; } = 3;
    public double SlotWeight { get; init; } = 2;
    public double LabWeight { get; init; } = 2;
    public double CapacityWeight { get; init; } = 1;
}

public sealed record ConstructionResult(
    int SuccessCount,
    int FailedCount,
    List<int> FailedSectionIds);

public sealed class ConstructionSolver
{
    private readonly CandidateGenerator _candidateGen;
    private readonly ConstraintEvaluationService _constraintService;

    public ConstructionSolver(
        CandidateGenerator candidateGen,
        ConstraintEvaluationService constraintService)
    {
        _candidateGen = candidateGen;
        _constraintService = constraintService;
    }

    public ConstructionResult Solve(
        List<SectionSnapshot> targetSections,
        MutableScheduleState state,
        List<Classroom> classrooms,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        SchedulingConstraintConfig config,
        ConstraintState constraintState,
        TermSnapshot baseSnapshot,
        HashSet<int> facultyClassroomIds,
        int splitThreshold,
        int backtrackLimit,
        MrvWeights mrvWeights,
        int softLookaheadCount = 1)
    {
        var classroomLookup = classrooms.ToDictionary(c => c.Id);
        var sectionLookup = targetSections.ToDictionary(s => s.SectionId);
        var domainStore = new DomainStore();
        var conflictStats = new ConflictStatistics();

        // Faz 3: Section difficulty — büyük zorunlu section'lar önce yerleşir.
        // CENG 384 (88 kişi, sadece AMFİ 1 + C 018 yeter) → MAT 121 (86, daha çok seçenek)
        // önünde sıraya girer. AMFİ 1 doğru section'a verilir, küçük section'lar küçük odalara.
        var difficultyScorer = new SectionDifficultyScorer();
        var difficulty = difficultyScorer.ScoreAll(targetSections, classrooms, facultyClassroomIds);

        // Step 1: Initialize domains — generate candidates for each section
        foreach (var section in targetSections)
        {
            var candidates = _candidateGen.Generate(
                section, state, classrooms, timeSlotsByDay,
                splitThreshold, facultyClassroomIds,
                splittableBlockTypes: config.SplittableBlockTypes);
            domainStore.InitDomain(section.SectionId, section, candidates);
        }

        // Step 2: Main assignment loop
        var assignedIds = new HashSet<int>();
        var failedSectionIds = new List<int>();

        while (true)
        {
            // Get unassigned sections ordered by MRV (smallest domain first)
            var unassigned = domainStore.GetUnassignedByMrv(assignedIds);
            if (unassigned.Count == 0) break;

            // Faz 3+: criticality-aware MRV.
            // Eski: smallest domain first, sonra difficulty tiebreak (eşit domain'de).
            // Yeni: criticality = difficulty / domain_size — kaynağı dar OLAN ZOR section'lar önce.
            // Bu paralel split gerekenleri ÖNCE yerleştirir, kolay section'lar sonra dolar.
            var nextSectionId = unassigned
                .OrderByDescending(id =>
                {
                    var diff = difficulty.GetValueOrDefault(id, 0);
                    var domain = Math.Max(1, domainStore.DomainSize(id));
                    return diff / (double)domain;
                })
                .ThenBy(id => domainStore.DomainSize(id))                       // ikincil: küçük domain
                .ThenByDescending(id => conflictStats.GetFailCount(id))         // üçüncül: eskiden fail
                .First();

            var section = sectionLookup[nextSectionId];

            // Get candidates ordered by least conflicting
            var candidates = conflictStats.OrderByLeastConflicting(
                nextSectionId, domainStore.GetCandidates(nextSectionId));

            // Empty domain → mark as failed
            if (candidates.Count == 0)
            {
                failedSectionIds.Add(nextSectionId);
                assignedIds.Add(nextSectionId); // treat as "processed"
                continue;
            }

            // ── Faz 1.3: Best-Fit Construction ──
            // Eskiden: ilk hard-pass eden candidate seçiliyordu (soft skor görmezden geliniyordu).
            // Yeni: top-K hard-pass eden candidate'ı topla, en az soft penalty vereni seç.
            //       FC en sona ertelenir (B-yolu, performans için). FC fail olursa 2.'ye düş.
            var lookahead = Math.Max(1, softLookaheadCount);
            var feasibleScored = new List<(BlockCombination Combo, List<BlockAssignment> Blocks, double SoftPenalty)>();

            foreach (var combo in candidates)
            {
                var blocks = new List<BlockAssignment>();
                if (combo.TheoryBlock != null) blocks.Add(combo.TheoryBlock);
                blocks.AddRange(combo.LabBlocks);
                if (blocks.Count == 0) continue;

                state.Assign(nextSectionId, section, blocks);

                var rawSnap = state.ToSectionSnapshot(nextSectionId);
                var sectionSnap = EnrichWithClassroomData(rawSnap, blocks, classroomLookup);
                var termSnap = state.ToTermSnapshot(baseSnapshot);
                var filteredSnapshot = BuildFilteredSnapshot(termSnap, nextSectionId);

                var (conflicts, _, softPenalty) = _constraintService.EvaluateCandidate(
                    sectionSnap, filteredSnapshot, config, constraintState);

                state.Unassign(nextSectionId);

                if (conflicts.Count > 0)
                {
                    foreach (var c in conflicts)
                    {
                        conflictStats.RecordConflict(nextSectionId, c.ConflictType, c.Day, c.StartTime);
                    }
                    continue;
                }

                feasibleScored.Add((combo, blocks, softPenalty));
                if (feasibleScored.Count >= lookahead) break;
            }

            // En düşük soft penalty veren candidate'tan başlayarak FC dene; ilk geçen seçilir.
            var placed = false;
            foreach (var entry in feasibleScored.OrderBy(x => x.SoftPenalty))
            {
                state.Assign(nextSectionId, section, entry.Blocks);

                var fcResult = domainStore.ForwardCheck(
                    nextSectionId, entry.Blocks, section, assignedIds,
                    (otherId, otherCombo) => IsConflicting(
                        otherId, otherCombo, sectionLookup, state, classroomLookup,
                        baseSnapshot, config, constraintState));

                if (fcResult.WipedOut)
                {
                    domainStore.UndoLastPrune();
                    state.Unassign(nextSectionId);
                    continue;
                }

                assignedIds.Add(nextSectionId);
                placed = true;
                break;
            }

            if (!placed)
            {
                failedSectionIds.Add(nextSectionId);
                assignedIds.Add(nextSectionId);
            }
        }

        var successCount = assignedIds.Count - failedSectionIds.Count;
        return new ConstructionResult(
            SuccessCount: successCount,
            FailedCount: failedSectionIds.Count,
            FailedSectionIds: failedSectionIds);
    }

    /// <summary>
    /// Checks whether a candidate BlockCombination for a given section would cause
    /// hard constraint violations. Used as the callback for DomainStore.ForwardCheck.
    /// </summary>
    private bool IsConflicting(
        int sectionId,
        BlockCombination combo,
        Dictionary<int, SectionSnapshot> sectionLookup,
        MutableScheduleState state,
        Dictionary<int, Classroom> classroomLookup,
        TermSnapshot baseSnapshot,
        SchedulingConstraintConfig config,
        ConstraintState constraintState)
    {
        if (!sectionLookup.TryGetValue(sectionId, out var section))
            return true;

        // Skip if section is already assigned (pinned or previously placed)
        if (state.IsAssigned(sectionId))
            return false;

        var blocks = new List<BlockAssignment>();
        if (combo.TheoryBlock != null) blocks.Add(combo.TheoryBlock);
        blocks.AddRange(combo.LabBlocks);
        if (blocks.Count == 0) return true;

        // Quick overlap check: if any block overlaps with an existing assignment
        // in the same classroom or same instructor, it's conflicting.
        // This avoids expensive full constraint evaluation for obvious conflicts.
        foreach (var block in blocks)
        {
            if (block.ClassroomId is int classroomId)
            {
                var occupancy = state.GetClassroomOccupancy(classroomId, block.Day);
                foreach (var (_, start, end, _) in occupancy)
                {
                    if (block.StartTime < end && block.EndTime > start)
                        return true;
                }
            }

            if (section.InstructorId.HasValue)
            {
                var instrOccupancy = state.GetInstructorOccupancy(section.InstructorId.Value, block.Day);
                foreach (var (_, start, end, _) in instrOccupancy)
                {
                    if (block.StartTime < end && block.EndTime > start)
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Builds a TermSnapshot that excludes the given section, for constraint evaluation.
    /// SlotAllowedModes MUST be preserved; H17 FaceToFaceSlotRestrictionEvaluator bu dict'e bakıyor,
    /// boş kalırsa evaluator bypass olur ve yüz-yüze dersler OnlineOnly slot'lara yerleşebilir.
    /// </summary>
    private static TermSnapshot BuildFilteredSnapshot(TermSnapshot snapshot, int excludeSectionId)
    {
        return new TermSnapshot
        {
            TermId = snapshot.TermId,
            TermName = snapshot.TermName,
            ScopeName = snapshot.ScopeName,
            Sections = snapshot.Sections.Where(s => s.SectionId != excludeSectionId).ToList(),
            ClassroomAssignments = snapshot.ClassroomAssignments,
            DeptFacultyMap = snapshot.DeptFacultyMap,
            Corequisites = snapshot.Corequisites,
            CoEnrollmentPairs = snapshot.CoEnrollmentPairs,
            CohortConflictPairs = snapshot.CohortConflictPairs,
            EnrollmentConflicts = snapshot.EnrollmentConflicts,
            DistributionConstraints = snapshot.DistributionConstraints,
            CourseSuggestedSemestersByDepartment = snapshot.CourseSuggestedSemestersByDepartment,
            CourseAccessibleDepartments = snapshot.CourseAccessibleDepartments,
            SlotAllowedModes = snapshot.SlotAllowedModes,
        };
    }

    /// <summary>
    /// Enriches a SectionSnapshot with correct classroom capacity and type
    /// based on the actual block assignments.
    /// Paralel split durumunda (aynı saat birden fazla farklı oda) kapasite toplanır —
    /// böylece H3 CapacityEvaluator toplam kapasiteyi section kontenjanıyla karşılaştırır.
    /// </summary>
    private static SectionSnapshot EnrichWithClassroomData(
        SectionSnapshot snap,
        List<BlockAssignment> blocks,
        Dictionary<int, Classroom> classroomLookup)
    {
        if (blocks.Count == 0) return snap;

        // Use the first block's classroom for the primary ClassroomName/Type fields
        // Online bloklarda ClassroomId null — enrichment yapılmaz, snap olduğu gibi döner
        var primaryClassroomId = blocks[0].ClassroomId;
        if (!primaryClassroomId.HasValue || !classroomLookup.TryGetValue(primaryClassroomId.Value, out var primaryClassroom))
            return snap;

        // Paralel split tespiti: aynı (Day, StartTime, EndTime) penceresinde BİRDEN FAZLA
        // farklı classroom varsa bunlar paralel çalışıyor demektir — toplam kapasite = odaların toplamı.
        // Farklı pencerelerdeki block'lar (örn. teori + lab farklı saatlerde) ayrı değerlendirilir;
        // burada "primary window" için paralel kapasiteyi hesaplıyoruz (en büyük oda bazlı).
        var primaryWindowBlocks = blocks
            .Where(b => b.Day == blocks[0].Day && b.StartTime == blocks[0].StartTime && b.EndTime == blocks[0].EndTime)
            .ToList();

        var combinedCapacity = 0;
        foreach (var block in primaryWindowBlocks)
        {
            if (block.ClassroomId.HasValue && classroomLookup.TryGetValue(block.ClassroomId.Value, out var cr))
                combinedCapacity += cr.Capacity;
        }

        // Eğer paralel split yoksa combinedCapacity == primaryClassroom.Capacity olur (fallback).
        var effectiveCapacity = combinedCapacity > 0 ? combinedCapacity : primaryClassroom.Capacity;

        return new SectionSnapshot
        {
            SectionId = snap.SectionId,
            SectionNumber = snap.SectionNumber,
            CourseId = snap.CourseId,
            CourseCode = snap.CourseCode,
            CourseName = snap.CourseName,
            TermId = snap.TermId,
            TheoryHours = snap.TheoryHours,
            LabHours = snap.LabHours,
            Capacity = snap.Capacity,
            EnrolledCount = snap.EnrolledCount,
            ClassroomId = snap.ClassroomId,
            ClassroomName = primaryClassroom.Name,
            ClassroomCapacity = effectiveCapacity,
            ClassroomType = primaryClassroom.Type,
            TheoryClassroomType = snap.TheoryClassroomType,
            LabClassroomType = snap.LabClassroomType,
            InstructorId = snap.InstructorId,
            InstructorName = snap.InstructorName,
            InstructorMaxWeeklyHours = snap.InstructorMaxWeeklyHours,
            DepartmentId = snap.DepartmentId,
            SuggestedSemester = snap.SuggestedSemester,
            SuggestedSemestersByDepartment = snap.SuggestedSemestersByDepartment,
            AccessibleDepartmentIds = snap.AccessibleDepartmentIds,
            AssignedTimeSlotCount = snap.AssignedTimeSlotCount,
            TimeBlocks = snap.TimeBlocks,
            Notes = snap.Notes
        };
    }
}
