using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Services;

namespace SIS.Infrastructure.Services.Scheduling.Legacy;

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
        int softLookaheadCount = 1,
        int perturbationSeed = 0,
        ScheduleProgressCallback? progressCallback = null,
        int progressStartPercent = 10,
        int progressEndPercent = 35)
    {
        var classroomLookup = classrooms.ToDictionary(c => c.Id);
        var sectionLookup = targetSections.ToDictionary(s => s.SectionId);
        var domainStore = new DomainStore();
        var conflictStats = new ConflictStatistics();
        // Progress throttle — peş peşe event yağmurunu engellemek için minimum 200ms.
        var lastProgressEmit = DateTime.MinValue;
        void EmitProgress(int processed, int total, string? detail = null)
        {
            if (progressCallback == null) return;
            var now = DateTime.UtcNow;
            if ((now - lastProgressEmit).TotalMilliseconds < 200) return;
            lastProgressEmit = now;
            var local = total > 0 ? Math.Min(100, processed * 100 / total) : 0;
            var globalPercent = progressStartPercent + (local * (progressEndPercent - progressStartPercent) / 100);
            progressCallback("construction", globalPercent, processed, total, detail);
        }
        // Multi-restart için perturbation: tie-break'lerde küçük random terim ile
        // section ordering'i çeşitlendirir. perturbationSeed=0 → deterministik (eski davranış).
        var perturbRng = perturbationSeed != 0 ? new Random(perturbationSeed) : null;
        var perturbScores = perturbRng != null
            ? targetSections.ToDictionary(s => s.SectionId, _ => perturbRng.NextDouble())
            : null;

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
            // Multi-restart için yumuşak perturbation: criticality'ye ±%5 noise.
            // Daha güçlü değer (±%15) section ordering'i bozup başarıyı düşürdü; bu seviye
            // restart'lar arası çeşitlilik sağlamak için yeterli, yapısal kararı bozmayacak kadar küçük.
            var nextSectionId = unassigned
                .OrderByDescending(id =>
                {
                    var diff = difficulty.GetValueOrDefault(id, 0);
                    var domain = Math.Max(1, domainStore.DomainSize(id));
                    var baseCrit = diff / (double)domain;
                    if (perturbScores == null) return baseCrit;
                    var noise = (perturbScores[id] - 0.5) * 0.10 * baseCrit; // ±%5
                    return baseCrit + noise;
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

            // ── Faz 1.3 + Bug A fix: Adaptive Best-Fit ──
            // Eski davranış: ilk K hard-pass candidate alınıp K'sının da FC fail etmesi durumunda
            // section fail ediyordu (kalan hard-pass candidate'lar görünmez kalıyordu).
            // Yeni: hard-pass candidate'ları batch-batch topla. Bir batch'in tümü FC wipeout
            // ederse batch'i 2× büyüt, candidate listesi tükenene kadar devam et.
            // CENG 102 gibi yüksek-yüklü section'lar paralel split combo'larında top-K aynı
            // (gün, saat) penceresinde yoğunlaştığında bu sayede 11.+ candidate'a ulaşır.
            var initialLookahead = Math.Max(1, softLookaheadCount);
            var feasibleScored = new List<(BlockCombination Combo, List<BlockAssignment> Blocks, double SoftPenalty)>();
            var triedFcEntries = new HashSet<int>(); // feasibleScored index'leri (FC denenenler)
            var nextCandIdx = 0;
            var batchTarget = initialLookahead;
            var placed = false;

            while (!placed)
            {
                // 1) Hard-pass candidate'ları batchTarget'a kadar topla.
                while (feasibleScored.Count < batchTarget && nextCandIdx < candidates.Count)
                {
                    var combo = candidates[nextCandIdx++];
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
                }

                // 2) Hiç hard-pass yoksa veya hepsi denendiyse → çık.
                if (feasibleScored.Count == 0 || feasibleScored.Count == triedFcEntries.Count)
                    break;

                // 3) Bu batch'in henüz denenmemiş entry'lerini soft-best sırayla FC dene.
                var orderedIndices = feasibleScored
                    .Select((entry, idx) => (Entry: entry, Idx: idx))
                    .Where(p => !triedFcEntries.Contains(p.Idx))
                    .OrderBy(p => p.Entry.SoftPenalty)
                    .ToList();

                foreach (var (entry, idx) in orderedIndices)
                {
                    triedFcEntries.Add(idx);

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

                if (placed) break;

                // 4) Bu batch tümüyle FC fail etti. Candidate listesi tükendiyse bırak,
                //    yoksa batch'i 2× büyütüp yeni hard-pass'lar topla.
                if (nextCandIdx >= candidates.Count) break;
                batchTarget = Math.Min(candidates.Count, batchTarget * 2);
            }

            if (!placed)
            {
                failedSectionIds.Add(nextSectionId);
                assignedIds.Add(nextSectionId);
            }

            // Progress: section'ın atandığı veya fail edildiği her noktada (throttle 200ms).
            // assignedIds.Count = işlenen toplam (placed + failed-as-processed).
            var placedSoFar = assignedIds.Count - failedSectionIds.Count;
            var progressDetail = failedSectionIds.Count == 0
                ? $"{placedSoFar}/{targetSections.Count} ders yerleştirildi"
                : $"{placedSoFar} yerleşti, {failedSectionIds.Count} başarısız ({assignedIds.Count}/{targetSections.Count})";
            EmitProgress(assignedIds.Count, targetSections.Count, progressDetail);
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
            AdjacentYearSoftPairs = snapshot.AdjacentYearSoftPairs,
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

        // Block'ları TimeBlock'a çevir, factory üzerinden snapshot oluştur.
        // Factory paralel-split capacity hesabını ve totalHours'ı tek otoriteden yapar.
        var timeBlocks = blocks.Select(b => new TimeBlock
        {
            Day = b.Day,
            StartTime = b.StartTime,
            EndTime = b.EndTime,
            ClassroomId = b.ClassroomId,
        }).ToList();

        return SectionSnapshotFactory.WithBlocks(snap, timeBlocks, classroomLookup);
    }
}
