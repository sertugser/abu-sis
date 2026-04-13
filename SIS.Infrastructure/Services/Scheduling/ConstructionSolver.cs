using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;

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
        // Build classroom lookup for enriching snapshots
        var classroomLookup = classrooms.ToDictionary(c => c.Id);

        // Step 1: Calculate MRV scores and sort descending (most constrained first)
        var maxCapacity = targetSections.Count > 0
            ? targetSections.Max(s => s.Capacity)
            : 1;
        if (maxCapacity == 0) maxCapacity = 1;

        var eligibleClassroomCounts = new Dictionary<int, int>();
        foreach (var section in targetSections)
        {
            var count = classrooms.Count(c =>
                facultyClassroomIds.Contains(c.Id) &&
                c.Capacity >= section.Capacity &&
                (c.Type == (section.TheoryClassroomType ?? Domain.Enums.ClassroomType.Lecture) ||
                 c.Type == (section.LabClassroomType ?? Domain.Enums.ClassroomType.Lab)));
            eligibleClassroomCounts[section.SectionId] = Math.Max(count, 1);
        }

        var sorted = targetSections
            .Select(s =>
            {
                var totalHours = s.TheoryHours + s.LabHours;
                var divisor = Math.Max(9 - totalHours, 1);
                var score =
                    mrvWeights.ClassroomWeight * (1.0 / eligibleClassroomCounts[s.SectionId]) +
                    mrvWeights.SlotWeight * (1.0 / divisor) +
                    mrvWeights.LabWeight * (s.LabHours > 0 ? 1.0 : 0.0) +
                    mrvWeights.CapacityWeight * ((double)s.Capacity / maxCapacity);
                return (Section: s, Score: score);
            })
            .OrderByDescending(x => x.Score)
            .Select(x => x.Section)
            .ToList();

        // Step 2: Track assignments for backtracking
        var assignmentHistory = new List<(int SectionId, List<BlockAssignment> Blocks, HashSet<BlockAssignment> ExcludedBlocks)>();
        var successIds = new HashSet<int>();
        var failedSectionIds = new List<int>();

        // Step 3: Try to place each section
        for (var i = 0; i < sorted.Count; i++)
        {
            var section = sorted[i];

            if (TryPlaceSection(section, state, classrooms, classroomLookup, timeSlotsByDay, config,
                constraintState, baseSnapshot, facultyClassroomIds, splitThreshold, null,
                out var blocks))
            {
                // TryPlaceSection leaves the assignment in state on success
                assignmentHistory.Add((section.SectionId, blocks!, new HashSet<BlockAssignment>()));
                successIds.Add(section.SectionId);
            }
            else
            {
                // Backtracking: try undoing previous non-pinned assignments
                var placed = TryBacktrackAndPlace(
                    section, state, classrooms, classroomLookup, timeSlotsByDay, config,
                    constraintState, baseSnapshot, facultyClassroomIds, splitThreshold,
                    backtrackLimit, assignmentHistory, successIds);

                if (!placed)
                {
                    failedSectionIds.Add(section.SectionId);
                }
            }
        }

        return new ConstructionResult(
            SuccessCount: successIds.Count,
            FailedCount: failedSectionIds.Count,
            FailedSectionIds: failedSectionIds);
    }

    /// <summary>
    /// Tries all candidate placements for a section. On success, leaves the section
    /// assigned in state and sets resultBlocks. On failure, state is unchanged.
    /// </summary>
    private bool TryPlaceSection(
        SectionSnapshot section,
        MutableScheduleState state,
        List<Classroom> classrooms,
        Dictionary<int, Classroom> classroomLookup,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        SchedulingConstraintConfig config,
        ConstraintState constraintState,
        TermSnapshot baseSnapshot,
        HashSet<int> facultyClassroomIds,
        int splitThreshold,
        HashSet<BlockAssignment>? excludeBlocks,
        out List<BlockAssignment>? resultBlocks)
    {
        resultBlocks = null;

        var candidates = _candidateGen.Generate(
            section, state, classrooms, timeSlotsByDay,
            splitThreshold, facultyClassroomIds, excludeBlocks);

        foreach (var combo in candidates)
        {
            var blocks = new List<BlockAssignment>();
            if (combo.TheoryBlock != null) blocks.Add(combo.TheoryBlock);
            if (combo.LabBlock != null) blocks.Add(combo.LabBlock);
            if (blocks.Count == 0) continue;

            // Temporarily assign to build snapshots
            state.Assign(section.SectionId, section, blocks);

            // Build enriched snapshot with correct classroom info
            var rawSnap = state.ToSectionSnapshot(section.SectionId);
            var sectionSnap = EnrichWithClassroomData(rawSnap, blocks, classroomLookup);

            var termSnap = state.ToTermSnapshot(baseSnapshot);

            // Build a filtered snapshot that excludes the candidate for evaluation
            var filteredSnapshot = new TermSnapshot
            {
                TermId = termSnap.TermId,
                TermName = termSnap.TermName,
                ScopeName = termSnap.ScopeName,
                Sections = termSnap.Sections.Where(s => s.SectionId != section.SectionId).ToList(),
                ClassroomAssignments = termSnap.ClassroomAssignments,
                DeptFacultyMap = termSnap.DeptFacultyMap,
                Corequisites = termSnap.Corequisites,
                CoEnrollmentPairs = termSnap.CoEnrollmentPairs,
                CourseSuggestedSemestersByDepartment = termSnap.CourseSuggestedSemestersByDepartment,
                CourseAccessibleDepartments = termSnap.CourseAccessibleDepartments,
            };

            var (conflicts, _, _) = _constraintService.EvaluateCandidate(
                sectionSnap, filteredSnapshot, config, constraintState);

            if (conflicts.Count == 0)
            {
                // Valid placement — keep it assigned
                resultBlocks = blocks;
                return true;
            }

            // Conflicts found — unassign and try next candidate
            state.Unassign(section.SectionId);
        }

        return false;
    }

    private bool TryBacktrackAndPlace(
        SectionSnapshot failedSection,
        MutableScheduleState state,
        List<Classroom> classrooms,
        Dictionary<int, Classroom> classroomLookup,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        SchedulingConstraintConfig config,
        ConstraintState constraintState,
        TermSnapshot baseSnapshot,
        HashSet<int> facultyClassroomIds,
        int splitThreshold,
        int backtrackLimit,
        List<(int SectionId, List<BlockAssignment> Blocks, HashSet<BlockAssignment> ExcludedBlocks)> assignmentHistory,
        HashSet<int> successIds)
    {
        for (var attempt = 0; attempt < backtrackLimit; attempt++)
        {
            // Find the last non-pinned assignment to undo
            var victimIndex = -1;
            for (var j = assignmentHistory.Count - 1; j >= 0; j--)
            {
                var (sid, _, _) = assignmentHistory[j];
                if (!state.IsPinned(sid) && successIds.Contains(sid))
                {
                    victimIndex = j;
                    break;
                }
            }

            if (victimIndex < 0) return false;

            var victim = assignmentHistory[victimIndex];
            var victimSection = state.GetPlacement(victim.SectionId)?.Snapshot;
            if (victimSection == null) return false;

            // Unassign victim
            state.Unassign(victim.SectionId);
            successIds.Remove(victim.SectionId);

            // Add victim's previous blocks to its exclude set
            foreach (var b in victim.Blocks)
                victim.ExcludedBlocks.Add(b);
            assignmentHistory[victimIndex] = victim;

            // Try to place the failed section
            if (TryPlaceSection(failedSection, state, classrooms, classroomLookup, timeSlotsByDay,
                config, constraintState, baseSnapshot, facultyClassroomIds, splitThreshold, null,
                out var failedBlocks))
            {
                // TryPlaceSection already assigned failedSection to state
                assignmentHistory.Add((failedSection.SectionId, failedBlocks!, new HashSet<BlockAssignment>()));
                successIds.Add(failedSection.SectionId);

                // Now try to re-place the victim with different blocks
                if (TryPlaceSection(victimSection, state, classrooms, classroomLookup, timeSlotsByDay,
                    config, constraintState, baseSnapshot, facultyClassroomIds, splitThreshold,
                    victim.ExcludedBlocks, out var victimBlocks))
                {
                    // TryPlaceSection already assigned victim to state
                    assignmentHistory[victimIndex] = (victim.SectionId, victimBlocks!, victim.ExcludedBlocks);
                    successIds.Add(victim.SectionId);
                    return true;
                }

                // Victim couldn't be re-placed, but failed section was placed successfully
                assignmentHistory.RemoveAt(victimIndex);
                return true;
            }

            // Failed section still couldn't be placed — restore victim
            if (TryPlaceSection(victimSection, state, classrooms, classroomLookup, timeSlotsByDay,
                config, constraintState, baseSnapshot, facultyClassroomIds, splitThreshold,
                victim.ExcludedBlocks, out var reBlocks))
            {
                // TryPlaceSection already assigned victim to state
                assignmentHistory[victimIndex] = (victim.SectionId, reBlocks!, victim.ExcludedBlocks);
                successIds.Add(victim.SectionId);
            }
            // Continue trying with the next victim
        }

        return false;
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
