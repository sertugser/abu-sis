using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class SimulationService : ISimulationService
{
    private readonly AppDbContext _context;
    private readonly SchedulingService _schedulingService;
    private readonly ConstraintEvaluationService _evaluationService;

    public SimulationService(
        AppDbContext context,
        SchedulingService schedulingService,
        ConstraintEvaluationService evaluationService)
    {
        _context = context;
        _schedulingService = schedulingService;
        _evaluationService = evaluationService;
    }

    public async Task<SimulationResult> SimulateMoveAsync(SimulateMoveRequest request, CancellationToken ct)
    {
        var (snapshot, config, state, weightMap) = await LoadEvaluationContextAsync(request.TermId, ct);

        // Evaluate BEFORE
        var beforeBreakdown = _evaluationService.EvaluateWithBreakdown(snapshot, config, state, weightMap);
        var before = new SimulationBreakdown(beforeBreakdown.TotalHardViolations, beforeBreakdown.TotalSoftPenalty);

        // Create modified snapshot with the virtual move
        var modifiedSnapshot = CloneSnapshotWithMove(snapshot, request);

        // Evaluate AFTER
        var afterBreakdown = _evaluationService.EvaluateWithBreakdown(modifiedSnapshot, config, state, weightMap);
        var after = new SimulationBreakdown(afterBreakdown.TotalHardViolations, afterBreakdown.TotalSoftPenalty);

        return BuildSimulationResult(before, after, beforeBreakdown, afterBreakdown);
    }

    public async Task<IReadOnlyList<SuggestionItem>> GetSuggestionsAsync(
        int sectionId, int termId, int limit, CancellationToken ct)
    {
        var (snapshot, config, state, _) = await LoadEvaluationContextAsync(termId, ct);

        var section = snapshot.Sections.FirstOrDefault(s => s.SectionId == sectionId);
        if (section == null) return Array.Empty<SuggestionItem>();

        // Evaluate current state for this section
        var filteredSnapshot = BuildFilteredSnapshot(snapshot, sectionId);
        var currentResult = _evaluationService.EvaluateCandidate(section, filteredSnapshot, config, state);
        var currentHard = currentResult.Conflicts.Count;
        var currentPenalty = currentResult.SoftPenalty;

        // Load available classrooms (limit for performance)
        var classrooms = await _context.Classrooms.AsNoTracking()
            .Where(c => c.Capacity >= section.Capacity)
            .OrderBy(c => Math.Abs(c.Capacity - section.Capacity))
            .Take(15)
            .Select(c => new { c.Id, c.Name, c.Capacity })
            .ToListAsync(ct);

        // Load time slots for this term
        var timeSlots = await _context.TimeSlots.AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .Select(ts => new { ts.DayOfWeek, ts.StartTime, ts.EndTime })
            .Distinct()
            .ToListAsync(ct);

        // Determine required block count from current section
        var requiredBlockCount = section.TimeBlocks.Count;
        if (requiredBlockCount == 0) requiredBlockCount = section.TheoryHours + section.LabHours;
        if (requiredBlockCount == 0) requiredBlockCount = 1;

        // Group time slots by day, find consecutive slot combinations
        var slotsByDay = timeSlots
            .GroupBy(ts => ts.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.OrderBy(ts => ts.StartTime).ToList());

        var suggestions = new List<SuggestionItem>();

        foreach (var (day, daySlots) in slotsByDay)
        {
            // Generate starting positions for consecutive blocks
            for (var startIdx = 0; startIdx <= daySlots.Count - requiredBlockCount; startIdx++)
            {
                var candidateSlots = daySlots.Skip(startIdx).Take(requiredBlockCount).ToList();
                var startTime = candidateSlots[0].StartTime;

                foreach (var classroom in classrooms)
                {
                    var newTimeBlocks = candidateSlots.Select(ts => new TimeBlock
                    {
                        Day = ts.DayOfWeek,
                        StartTime = ts.StartTime,
                        EndTime = ts.EndTime,
                        ClassroomId = classroom.Id,
                    }).ToList();

                    // Build a candidate section snapshot with the new time/classroom
                    var movedSection = CloneSectionWithNewSchedule(
                        section, newTimeBlocks, classroom.Id, classroom.Name, classroom.Capacity);

                    var result = _evaluationService.EvaluateCandidate(movedSection, filteredSnapshot, config, state);

                    // Only include feasible (no hard violations) or better-than-current suggestions
                    if (result.Conflicts.Count <= currentHard)
                    {
                        var improvement = currentPenalty - result.SoftPenalty;
                        suggestions.Add(new SuggestionItem(
                            0,
                            (int)day,
                            startTime.ToString("HH:mm"),
                            classroom.Id,
                            classroom.Name,
                            result.Conflicts.Count,
                            Math.Round(result.SoftPenalty, 1),
                            Math.Round(improvement, 1),
                            result.Notes.Take(3).ToList()));
                    }
                }
            }
        }

        // Sort: fewer hard violations first, then lower penalty, assign ranks
        return suggestions
            .OrderBy(s => s.HardViolations)
            .ThenBy(s => s.SoftPenalty)
            .Take(limit)
            .Select((s, i) => s with { Rank = i + 1 })
            .ToList();
    }

    public async Task<SimulationResult> SimulateConstraintChangeAsync(
        SimulateConstraintChangeRequest request, CancellationToken ct)
    {
        var (snapshot, config, state, weightMap) = await LoadEvaluationContextAsync(request.TermId, ct);

        // Evaluate BEFORE
        var beforeBreakdown = _evaluationService.EvaluateWithBreakdown(snapshot, config, state, weightMap);
        var before = new SimulationBreakdown(beforeBreakdown.TotalHardViolations, beforeBreakdown.TotalSoftPenalty);

        // Apply virtual changes to constraint definitions (in-memory only)
        foreach (var change in request.Changes)
        {
            var def = state.Definitions.Values.FirstOrDefault(d => d.Id == change.ConstraintId);
            if (def == null) continue;

            switch (change.Field.ToLowerInvariant())
            {
                case "isenabled":
                    def.IsEnabled = Convert.ToBoolean(change.Value);
                    break;
                case "weight":
                    var newWeight = Convert.ToInt32(change.Value);
                    def.Weight = newWeight;
                    weightMap[def.Code] = newWeight;
                    break;
                case "parameters":
                    def.Parameters = change.Value?.ToString();
                    break;
            }
        }

        // Evaluate AFTER with modified state
        var afterBreakdown = _evaluationService.EvaluateWithBreakdown(snapshot, config, state, weightMap);
        var after = new SimulationBreakdown(afterBreakdown.TotalHardViolations, afterBreakdown.TotalSoftPenalty);

        return BuildSimulationResult(before, after, beforeBreakdown, afterBreakdown);
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private async Task<(TermSnapshot Snapshot, SchedulingConstraintConfig Config, ConstraintState State, Dictionary<string, int> WeightMap)>
        LoadEvaluationContextAsync(int termId, CancellationToken ct)
    {
        var snapshot = await _schedulingService.BuildTermSnapshotAsync(termId, null, null, ct);
        var softConfig = await _schedulingService.LoadSoftConstraintConfigAsync(ct);
        var hardConfig = await _schedulingService.LoadHardConstraintConfigAsync(ct);

        if (softConfig.S4Enabled)
            snapshot.CoEnrollmentPairs = await _schedulingService.LoadCoEnrollmentPairsAsync(
                snapshot.TermId, softConfig.S4MinCoPairThreshold, ct);

        var config = _schedulingService.BuildConstraintConfig(softConfig, hardConfig);
        var state = await _evaluationService.LoadConstraintStateAsync(null, null, null, termId, ct);
        var weightMap = state.Definitions.ToDictionary(d => d.Key, d => d.Value.Weight);

        return (snapshot, config, state, weightMap);
    }

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
            CourseSuggestedSemestersByDepartment = snapshot.CourseSuggestedSemestersByDepartment,
            CourseAccessibleDepartments = snapshot.CourseAccessibleDepartments,
            DistributionConstraints = snapshot.DistributionConstraints,
            SlotAllowedModes = snapshot.SlotAllowedModes,
        };
    }

    private static TermSnapshot CloneSnapshotWithMove(TermSnapshot original, SimulateMoveRequest move)
    {
        var newDay = (DayOfWeek)move.NewDayOfWeek;
        var newStart = TimeOnly.Parse(move.NewTimeSlotStart);

        var sections = original.Sections.Select(s =>
        {
            if (s.SectionId != move.SectionId) return s;

            // Calculate new time blocks by shifting all blocks to the new day/start time
            var duration = s.TimeBlocks.Count > 0
                ? s.TimeBlocks[0].EndTime - s.TimeBlocks[0].StartTime
                : TimeSpan.FromMinutes(50);

            var newBlocks = new List<TimeBlock>();
            var currentStart = newStart;
            var blockCount = s.TimeBlocks.Count > 0 ? s.TimeBlocks.Count : Math.Max(s.TheoryHours + s.LabHours, 1);

            for (var i = 0; i < blockCount; i++)
            {
                newBlocks.Add(new TimeBlock
                {
                    Day = newDay,
                    StartTime = currentStart,
                    EndTime = currentStart.Add(duration),
                    ClassroomId = move.NewClassroomId ?? s.ClassroomId,
                });
                // Next block starts after a 10-minute break
                currentStart = currentStart.Add(duration).Add(TimeSpan.FromMinutes(10));
            }

            var classroomId = move.NewClassroomId ?? s.ClassroomId;

            return new SectionSnapshot
            {
                SectionId = s.SectionId,
                SectionNumber = s.SectionNumber,
                CourseId = s.CourseId,
                CourseCode = s.CourseCode,
                CourseName = s.CourseName,
                TermId = s.TermId,
                TheoryHours = s.TheoryHours,
                LabHours = s.LabHours,
                Capacity = s.Capacity,
                EnrolledCount = s.EnrolledCount,
                ClassroomId = classroomId,
                ClassroomName = classroomId != s.ClassroomId ? $"Derslik #{classroomId}" : s.ClassroomName,
                ClassroomCapacity = s.ClassroomCapacity,
                ClassroomType = s.ClassroomType,
                TheoryClassroomType = s.TheoryClassroomType,
                LabClassroomType = s.LabClassroomType,
                InstructorId = s.InstructorId,
                InstructorName = s.InstructorName,
                InstructorMaxWeeklyHours = s.InstructorMaxWeeklyHours,
                DepartmentId = s.DepartmentId,
                SuggestedSemester = s.SuggestedSemester,
                SuggestedSemestersByDepartment = s.SuggestedSemestersByDepartment,
                AccessibleDepartmentIds = s.AccessibleDepartmentIds,
                AssignedTimeSlotCount = newBlocks.Count,
                TimeBlocks = newBlocks,
                Notes = new List<string>(),
            };
        }).ToList();

        return new TermSnapshot
        {
            TermId = original.TermId,
            TermName = original.TermName,
            ScopeName = original.ScopeName,
            Sections = sections,
            ClassroomAssignments = original.ClassroomAssignments,
            DeptFacultyMap = original.DeptFacultyMap,
            Corequisites = original.Corequisites,
            CoEnrollmentPairs = original.CoEnrollmentPairs,
            CohortConflictPairs = original.CohortConflictPairs,
            AdjacentYearSoftPairs = original.AdjacentYearSoftPairs,
            EnrollmentConflicts = original.EnrollmentConflicts,
            CourseSuggestedSemestersByDepartment = original.CourseSuggestedSemestersByDepartment,
            CourseAccessibleDepartments = original.CourseAccessibleDepartments,
            DistributionConstraints = original.DistributionConstraints,
            SlotAllowedModes = original.SlotAllowedModes,
        };
    }

    private static SectionSnapshot CloneSectionWithNewSchedule(
        SectionSnapshot original,
        List<TimeBlock> newBlocks,
        int classroomId,
        string classroomName,
        int classroomCapacity)
    {
        return new SectionSnapshot
        {
            SectionId = original.SectionId,
            SectionNumber = original.SectionNumber,
            CourseId = original.CourseId,
            CourseCode = original.CourseCode,
            CourseName = original.CourseName,
            TermId = original.TermId,
            TheoryHours = original.TheoryHours,
            LabHours = original.LabHours,
            Capacity = original.Capacity,
            EnrolledCount = original.EnrolledCount,
            ClassroomId = classroomId,
            ClassroomName = classroomName,
            ClassroomCapacity = classroomCapacity,
            ClassroomType = original.ClassroomType,
            TheoryClassroomType = original.TheoryClassroomType,
            LabClassroomType = original.LabClassroomType,
            InstructorId = original.InstructorId,
            InstructorName = original.InstructorName,
            InstructorMaxWeeklyHours = original.InstructorMaxWeeklyHours,
            DepartmentId = original.DepartmentId,
            SuggestedSemester = original.SuggestedSemester,
            SuggestedSemestersByDepartment = original.SuggestedSemestersByDepartment,
            AccessibleDepartmentIds = original.AccessibleDepartmentIds,
            AssignedTimeSlotCount = newBlocks.Count,
            TimeBlocks = newBlocks,
            Notes = new List<string>(),
        };
    }

    private static SimulationResult BuildSimulationResult(
        SimulationBreakdown before,
        SimulationBreakdown after,
        ScoreBreakdownDto beforeBreakdown,
        ScoreBreakdownDto afterBreakdown)
    {
        var beforeViolations = beforeBreakdown.Constraints
            .Where(c => c.ViolationCount > 0)
            .Select(c => $"{c.Code}: {c.Name} ({c.ViolationCount})")
            .ToHashSet();

        var afterViolations = afterBreakdown.Constraints
            .Where(c => c.ViolationCount > 0)
            .Select(c => $"{c.Code}: {c.Name} ({c.ViolationCount})")
            .ToHashSet();

        var resolved = beforeViolations.Except(afterViolations).ToList();
        var newConflicts = afterViolations.Except(beforeViolations).ToList();
        var penaltyDelta = Math.Round(after.SoftPenalty - before.SoftPenalty, 1);

        var netImprovement = after.HardViolations < before.HardViolations
            || (after.HardViolations == before.HardViolations && penaltyDelta < 0);

        return new SimulationResult(
            after.HardViolations == 0,
            before,
            after,
            new SimulationDiff(resolved, newConflicts, penaltyDelta, netImprovement));
    }
}
