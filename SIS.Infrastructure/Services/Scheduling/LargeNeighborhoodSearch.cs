using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed record LNSResult(
    int Iterations,
    int Improvements,
    int SectionsRecovered,
    double ElapsedSeconds);

public sealed class LargeNeighborhoodSearch
{
    private readonly CandidateGenerator _candidateGen;
    private readonly ConstraintEvaluationService _constraintService;

    public LargeNeighborhoodSearch(
        CandidateGenerator candidateGen,
        ConstraintEvaluationService constraintService)
    {
        _candidateGen = candidateGen;
        _constraintService = constraintService;
    }

    public LNSResult Search(
        MutableScheduleState state,
        List<SectionSnapshot> allTargetSnapshots,
        List<int> failedSectionIds,
        List<Classroom> classrooms,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        SchedulingConstraintConfig config,
        ConstraintState constraintState,
        TermSnapshot baseSnapshot,
        HashSet<int> facultyClassroomIds,
        int splitThreshold,
        int seed,
        int maxIterations,
        int timeoutSeconds,
        int destroySize,
        int softLookaheadCount = 1,
        Application.Scheduling.Services.ScheduleProgressCallback? progressCallback = null,
        int progressStartPercent = 70,
        int progressEndPercent = 90)
    {
        var random = new Random(seed);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var classroomLookup = classrooms.ToDictionary(c => c.Id);
        var improvements = 0;
        var recovered = 0;
        var iterations = 0;

        var nonPinnedIds = state.GetAssignedNonPinnedSectionIds();
        if (nonPinnedIds.Count < destroySize)
            return new LNSResult(0, 0, 0, 0);

        var snapLookup = allTargetSnapshots.ToDictionary(s => s.SectionId);

        var operators = new List<DestroyOperator>
        {
            new("random", 1.0, (s, r, ds) => RandomDestroy(s, r, ds, nonPinnedIds)),
            new("worst", 1.0, (s, r, ds) => WorstDestroy(s, r, ds, config, constraintState, baseSnapshot)),
            new("related", 1.0, (s, r, ds) => RelatedDestroy(s, r, ds)),
        };

        // Progress throttle — 200ms minimum aralık.
        var lastProgressEmit = DateTime.MinValue;
        var initialFailCount = failedSectionIds.Count;
        void EmitProgress()
        {
            if (progressCallback == null) return;
            var now = DateTime.UtcNow;
            if ((now - lastProgressEmit).TotalMilliseconds < 200) return;
            lastProgressEmit = now;
            var local = maxIterations > 0 ? Math.Min(100, iterations * 100 / maxIterations) : 0;
            var globalPercent = progressStartPercent + (local * (progressEndPercent - progressStartPercent) / 100);

            // Anlamlı detail metni — "0/100 iter, 0 ders" yerine kullanıcının önemsediği
            // mesaja odaklan. Iter sayısı global progress bar'a zaten yansıyor.
            string detail;
            if (recovered > 0)
                detail = $"{recovered}/{initialFailCount} ders kurtarıldı";
            else if (iterations < 5)
                detail = "Alternatif yerleşim aranıyor...";
            else
                detail = $"Alternatif yerleşim aranıyor ({iterations}. deneme)";

            progressCallback("lns", globalPercent, recovered, initialFailCount, detail);
        }

        for (iterations = 0; iterations < maxIterations; iterations++)
        {
            if (stopwatch.Elapsed.TotalSeconds > timeoutSeconds) break;
            // Periodic progress (her 5 iter veya 200ms — hangisi önce).
            if ((iterations % 5) == 0)
                EmitProgress();

            var op = SelectOperator(operators, random);
            var backup = state.Clone();
            var prevPlacedCount = state.GetAssignedNonPinnedSectionIds().Count;

            var destroyed = op.Execute(state, random, destroySize);
            if (destroyed.Count == 0) continue;

            var repairPool = new List<int>(destroyed);
            foreach (var fid in failedSectionIds)
            {
                if (!state.IsAssigned(fid)) repairPool.Add(fid);
            }

            GreedyRepair(repairPool, snapLookup, state, classrooms, classroomLookup,
                timeSlotsByDay, config, constraintState, baseSnapshot, facultyClassroomIds, splitThreshold, softLookaheadCount);

            var newPlacedCount = state.GetAssignedNonPinnedSectionIds().Count;

            if (newPlacedCount >= prevPlacedCount)
            {
                op.Weight = Math.Min(op.Weight * 1.1, 5.0);
                if (newPlacedCount > prevPlacedCount)
                {
                    improvements++;
                    recovered += newPlacedCount - prevPlacedCount;
                    failedSectionIds.RemoveAll(id => state.IsAssigned(id));
                }
                nonPinnedIds = state.GetAssignedNonPinnedSectionIds();
            }
            else
            {
                RestoreState(state, backup);
                op.Weight = Math.Max(op.Weight * 0.9, 0.2);
            }
        }

        stopwatch.Stop();
        return new LNSResult(iterations, improvements, recovered, stopwatch.Elapsed.TotalSeconds);
    }

    private static List<int> RandomDestroy(MutableScheduleState state, Random random, int size, List<int> nonPinnedIds)
    {
        var candidates = nonPinnedIds.Where(id => state.IsAssigned(id)).ToList();
        var toDestroy = candidates.OrderBy(_ => random.Next()).Take(Math.Min(size, candidates.Count)).ToList();
        foreach (var id in toDestroy) state.Unassign(id);
        return toDestroy;
    }

    private List<int> WorstDestroy(MutableScheduleState state, Random random, int size,
        SchedulingConstraintConfig config, ConstraintState constraintState, TermSnapshot baseSnapshot)
    {
        var termSnap = state.ToTermSnapshot(baseSnapshot);
        var penalties = new List<(int Id, double Penalty)>();

        foreach (var (id, placement) in state.AllPlacements)
        {
            if (placement.IsPinned) continue;
            var snap = state.ToSectionSnapshot(id);
            var filtered = BuildFilteredSnapshot(termSnap, id);
            var penalty = _constraintService.CalculatePenalty(snap, filtered, config, constraintState);
            penalties.Add((id, penalty));
        }

        var toDestroy = penalties.OrderByDescending(p => p.Penalty).Take(size).Select(p => p.Id).ToList();
        foreach (var id in toDestroy) state.Unassign(id);
        return toDestroy;
    }

    private static List<int> RelatedDestroy(MutableScheduleState state, Random random, int size)
    {
        var nonPinned = state.AllPlacements.Where(p => !p.Value.IsPinned).Select(p => p.Key).ToList();
        if (nonPinned.Count == 0) return new();

        var seedId = nonPinned[random.Next(nonPinned.Count)];
        var seedPlacement = state.GetPlacement(seedId);
        if (seedPlacement == null) return new();

        var seedDays = seedPlacement.Blocks.Select(b => b.Day).ToHashSet();
        var seedDeptId = seedPlacement.Snapshot.DepartmentId;

        var scored = nonPinned
            .Where(id => id != seedId && state.IsAssigned(id))
            .Select(id =>
            {
                var p = state.GetPlacement(id)!;
                var dayOverlap = p.Blocks.Count(b => seedDays.Contains(b.Day));
                var sameDept = (p.Snapshot.DepartmentId == seedDeptId) ? 2 : 0;
                return (Id: id, Score: dayOverlap + sameDept);
            })
            .OrderByDescending(x => x.Score)
            .Take(size - 1)
            .Select(x => x.Id)
            .ToList();

        scored.Add(seedId);
        foreach (var id in scored) state.Unassign(id);
        return scored;
    }

    private void GreedyRepair(List<int> sectionIds, Dictionary<int, SectionSnapshot> snapLookup,
        MutableScheduleState state, List<Classroom> classrooms, Dictionary<int, Classroom> classroomLookup,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay, SchedulingConstraintConfig config,
        ConstraintState constraintState, TermSnapshot baseSnapshot,
        HashSet<int> facultyClassroomIds, int splitThreshold, int softLookaheadCount = 1)
    {
        var ordered = sectionIds
            .Where(id => snapLookup.ContainsKey(id) && !state.IsAssigned(id))
            .Select(id =>
            {
                var snap = snapLookup[id];
                var cands = _candidateGen.Generate(snap, state, classrooms, timeSlotsByDay, splitThreshold, facultyClassroomIds,
                    splittableBlockTypes: config.SplittableBlockTypes);
                return (Id: id, Snap: snap, Candidates: cands);
            })
            .OrderBy(x => x.Candidates.Count)
            .ToList();

        var lookahead = Math.Max(1, softLookaheadCount);

        foreach (var (sectionId, section, candidates) in ordered)
        {
            if (state.IsAssigned(sectionId)) continue;

            // Faz 1.4: Best-Fit GreedyRepair — top-K hard-pass candidate'tan en az soft veren seçilir.
            var feasibleScored = new List<(BlockCombination Combo, List<BlockAssignment> Blocks, double SoftPenalty)>();

            foreach (var combo in candidates)
            {
                var blocks = new List<BlockAssignment>();
                if (combo.TheoryBlock != null) blocks.Add(combo.TheoryBlock);
                blocks.AddRange(combo.LabBlocks);
                if (blocks.Count == 0) continue;

                state.Assign(sectionId, section, blocks);

                var rawSnap = state.ToSectionSnapshot(sectionId);
                var enriched = EnrichWithClassroomData(rawSnap, blocks, classroomLookup);
                var termSnap = state.ToTermSnapshot(baseSnapshot);
                var filtered = BuildFilteredSnapshot(termSnap, sectionId);

                var (conflicts, _, softPenalty) = _constraintService.EvaluateCandidate(enriched, filtered, config, constraintState);

                state.Unassign(sectionId);

                if (conflicts.Count > 0) continue;

                feasibleScored.Add((combo, blocks, softPenalty));
                if (feasibleScored.Count >= lookahead) break;
            }

            if (feasibleScored.Count == 0) continue;

            var best = feasibleScored.OrderBy(x => x.SoftPenalty).First();
            state.Assign(sectionId, section, best.Blocks);
        }
    }

    private static TermSnapshot BuildFilteredSnapshot(TermSnapshot snapshot, int excludeSectionId)
    {
        return new TermSnapshot
        {
            TermId = snapshot.TermId, TermName = snapshot.TermName, ScopeName = snapshot.ScopeName,
            Sections = snapshot.Sections.Where(s => s.SectionId != excludeSectionId).ToList(),
            ClassroomAssignments = snapshot.ClassroomAssignments, DeptFacultyMap = snapshot.DeptFacultyMap,
            Corequisites = snapshot.Corequisites, CoEnrollmentPairs = snapshot.CoEnrollmentPairs,
            CourseSuggestedSemestersByDepartment = snapshot.CourseSuggestedSemestersByDepartment,
            CourseAccessibleDepartments = snapshot.CourseAccessibleDepartments,
            CohortConflictPairs = snapshot.CohortConflictPairs,
            AdjacentYearSoftPairs = snapshot.AdjacentYearSoftPairs,
            EnrollmentConflicts = snapshot.EnrollmentConflicts,
            DistributionConstraints = snapshot.DistributionConstraints,
            SlotAllowedModes = snapshot.SlotAllowedModes,
        };
    }

    private static SectionSnapshot EnrichWithClassroomData(SectionSnapshot snap, List<BlockAssignment> blocks, Dictionary<int, Classroom> classroomLookup)
    {
        if (blocks.Count == 0) return snap;
        var firstClassroomId = blocks[0].ClassroomId;
        if (!firstClassroomId.HasValue || !classroomLookup.TryGetValue(firstClassroomId.Value, out var cr)) return snap;
        return new SectionSnapshot
        {
            SectionId = snap.SectionId, SectionNumber = snap.SectionNumber,
            CourseId = snap.CourseId, CourseCode = snap.CourseCode, CourseName = snap.CourseName,
            TermId = snap.TermId, TheoryHours = snap.TheoryHours, LabHours = snap.LabHours,
            Capacity = snap.Capacity, EnrolledCount = snap.EnrolledCount,
            ClassroomId = snap.ClassroomId, ClassroomName = cr.Name,
            ClassroomCapacity = cr.Capacity, ClassroomType = cr.Type,
            TheoryClassroomType = snap.TheoryClassroomType, LabClassroomType = snap.LabClassroomType,
            InstructorId = snap.InstructorId, InstructorName = snap.InstructorName,
            InstructorMaxWeeklyHours = snap.InstructorMaxWeeklyHours,
            DepartmentId = snap.DepartmentId, SuggestedSemester = snap.SuggestedSemester,
            SuggestedSemestersByDepartment = snap.SuggestedSemestersByDepartment,
            AccessibleDepartmentIds = snap.AccessibleDepartmentIds,
            AssignedTimeSlotCount = snap.AssignedTimeSlotCount,
            TimeBlocks = snap.TimeBlocks, Notes = snap.Notes
        };
    }

    private static DestroyOperator SelectOperator(List<DestroyOperator> operators, Random random)
    {
        var totalWeight = operators.Sum(o => o.Weight);
        var roll = random.NextDouble() * totalWeight;
        var cumulative = 0.0;
        foreach (var op in operators)
        {
            cumulative += op.Weight;
            if (roll <= cumulative) return op;
        }
        return operators[^1];
    }

    private static void RestoreState(MutableScheduleState working, MutableScheduleState backup)
    {
        var nonPinned = working.GetAssignedNonPinnedSectionIds();
        foreach (var id in nonPinned) working.Unassign(id);
        var backupNonPinned = backup.GetAssignedNonPinnedSectionIds();
        foreach (var id in backupNonPinned)
        {
            var p = backup.GetPlacement(id);
            if (p != null) working.Assign(id, p.Snapshot, p.Blocks.ToList());
        }
    }

    private sealed class DestroyOperator
    {
        public string Name { get; }
        public double Weight { get; set; }
        private readonly Func<MutableScheduleState, Random, int, List<int>> _execute;
        public DestroyOperator(string name, double weight, Func<MutableScheduleState, Random, int, List<int>> execute)
        { Name = name; Weight = weight; _execute = execute; }
        public List<int> Execute(MutableScheduleState state, Random random, int size) => _execute(state, random, size);
    }
}
