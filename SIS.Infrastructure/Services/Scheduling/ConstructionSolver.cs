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
        MrvWeights mrvWeights)
    {
        var classroomLookup = classrooms.ToDictionary(c => c.Id);
        var sectionLookup = targetSections.ToDictionary(s => s.SectionId);
        var domainStore = new DomainStore();
        var conflictStats = new ConflictStatistics();

        // Step 1: Initialize domains — generate candidates for each section
        foreach (var section in targetSections)
        {
            var candidates = _candidateGen.Generate(
                section, state, classrooms, timeSlotsByDay,
                splitThreshold, facultyClassroomIds);
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

            // Tiebreak: among those with same domain size, pick most-failed first
            var bestDomainSize = domainStore.DomainSize(unassigned[0]);
            var nextSectionId = unassigned
                .TakeWhile(id => domainStore.DomainSize(id) == bestDomainSize)
                .OrderByDescending(id => conflictStats.GetFailCount(id))
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

            var placed = false;
            foreach (var combo in candidates)
            {
                var blocks = new List<BlockAssignment>();
                if (combo.TheoryBlock != null) blocks.Add(combo.TheoryBlock);
                blocks.AddRange(combo.LabBlocks);
                if (blocks.Count == 0) continue;

                // Temporarily assign
                state.Assign(nextSectionId, section, blocks);

                // Build enriched snapshot and evaluate hard constraints
                var rawSnap = state.ToSectionSnapshot(nextSectionId);
                var sectionSnap = EnrichWithClassroomData(rawSnap, blocks, classroomLookup);
                var termSnap = state.ToTermSnapshot(baseSnapshot);
                var filteredSnapshot = BuildFilteredSnapshot(termSnap, nextSectionId);

                var (conflicts, _, _) = _constraintService.EvaluateCandidate(
                    sectionSnap, filteredSnapshot, config, constraintState);

                if (conflicts.Count > 0)
                {
                    // Record each conflict for future ordering
                    foreach (var c in conflicts)
                    {
                        conflictStats.RecordConflict(nextSectionId, c.ConflictType, c.Day, c.StartTime);
                    }

                    state.Unassign(nextSectionId);
                    continue;
                }

                // No hard conflicts — run forward check
                var fcResult = domainStore.ForwardCheck(
                    nextSectionId, blocks, section, assignedIds,
                    (otherId, otherCombo) => IsConflicting(
                        otherId, otherCombo, sectionLookup, state, classroomLookup,
                        baseSnapshot, config, constraintState));

                if (fcResult.WipedOut)
                {
                    // Forward check caused a wipeout — undo prune and try next candidate
                    domainStore.UndoLastPrune();
                    state.Unassign(nextSectionId);
                    continue;
                }

                // Assignment successful
                assignedIds.Add(nextSectionId);
                placed = true;
                break;
            }

            if (!placed)
            {
                failedSectionIds.Add(nextSectionId);
                assignedIds.Add(nextSectionId); // treat as "processed"
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
            var occupancy = state.GetClassroomOccupancy(block.ClassroomId, block.Day);
            foreach (var (_, start, end, _) in occupancy)
            {
                if (block.StartTime < end && block.EndTime > start)
                    return true;
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
            CourseSuggestedSemestersByDepartment = snapshot.CourseSuggestedSemestersByDepartment,
            CourseAccessibleDepartments = snapshot.CourseAccessibleDepartments,
        };
    }

    /// <summary>
    /// Enriches a SectionSnapshot with correct classroom capacity and type
    /// based on the actual block assignments.
    /// </summary>
    private static SectionSnapshot EnrichWithClassroomData(
        SectionSnapshot snap,
        List<BlockAssignment> blocks,
        Dictionary<int, Classroom> classroomLookup)
    {
        if (blocks.Count == 0) return snap;

        // Use the first block's classroom for the primary ClassroomCapacity/Type fields
        var primaryClassroomId = blocks[0].ClassroomId;
        if (!classroomLookup.TryGetValue(primaryClassroomId, out var primaryClassroom))
            return snap;

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
            ClassroomCapacity = primaryClassroom.Capacity,
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
