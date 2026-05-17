using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class SimulationService : ISimulationService
{
    private readonly AppDbContext _context;
    private readonly SchedulingService _schedulingService;
    private readonly IConstraintStateLoader _constraintStateLoader;
    private readonly IRuleEngine _ruleEngine;

    public SimulationService(
        AppDbContext context,
        SchedulingService schedulingService,
        IConstraintStateLoader constraintStateLoader,
        IRuleEngine ruleEngine)
    {
        _context = context;
        _schedulingService = schedulingService;
        _constraintStateLoader = constraintStateLoader;
        _ruleEngine = ruleEngine;
    }

    public async Task<SimulationResult> SimulateMoveAsync(SimulateMoveRequest request, CancellationToken ct)
    {
        var (snapshot, config, state) = await LoadEvaluationContextAsync(request.TermId, ct);

        // BEFORE
        var beforeBreakdown = await _ruleEngine.EvaluateWithBreakdownAsync(snapshot, config, state, ct);
        var before = new SimulationBreakdown(beforeBreakdown.TotalHardViolations, beforeBreakdown.TotalSoftPenalty);

        // Modified snapshot with the virtual move
        var modifiedSnapshot = CloneSnapshotWithMove(snapshot, request);

        // AFTER
        var afterBreakdown = await _ruleEngine.EvaluateWithBreakdownAsync(modifiedSnapshot, config, state, ct);
        var after = new SimulationBreakdown(afterBreakdown.TotalHardViolations, afterBreakdown.TotalSoftPenalty);

        return BuildSimulationResult(before, after, beforeBreakdown, afterBreakdown);
    }

    public async Task<IReadOnlyList<SuggestionItem>> GetSuggestionsAsync(
        int sectionId, int termId, int limit, CancellationToken ct)
    {
        var (snapshot, config, state) = await LoadEvaluationContextAsync(termId, ct);

        var section = snapshot.Sections.FirstOrDefault(s => s.SectionId == sectionId);
        if (section == null) return Array.Empty<SuggestionItem>();

        var filteredSnapshot = BuildFilteredSnapshot(snapshot, sectionId);
        var currentHardConflicts = await _ruleEngine.EvaluateCandidateAsync(section, filteredSnapshot, config, state, ct);
        var currentPenalty = await _ruleEngine.CalculateCandidatePenaltyAsync(section, filteredSnapshot, config, state, ct);
        var currentHard = currentHardConflicts.Count;

        var classrooms = await _context.Classrooms.AsNoTracking()
            .Where(c => c.Capacity >= section.Capacity)
            .OrderBy(c => Math.Abs(c.Capacity - section.Capacity))
            .Take(15)
            .Select(c => new { c.Id, c.Name, c.Capacity })
            .ToListAsync(ct);

        var timeSlots = await _context.TimeSlots.AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .Select(ts => new { ts.DayOfWeek, ts.StartTime, ts.EndTime })
            .Distinct()
            .ToListAsync(ct);

        var requiredBlockCount = section.Blocks.Count;
        if (requiredBlockCount == 0) requiredBlockCount = section.TheoryHours + section.LabHours;
        if (requiredBlockCount == 0) requiredBlockCount = 1;

        var slotsByDay = timeSlots
            .GroupBy(ts => ts.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.OrderBy(ts => ts.StartTime).ToList());

        var suggestions = new List<SuggestionItem>();

        foreach (var (day, daySlots) in slotsByDay)
        {
            for (var startIdx = 0; startIdx <= daySlots.Count - requiredBlockCount; startIdx++)
            {
                var candidateSlots = daySlots.Skip(startIdx).Take(requiredBlockCount).ToList();
                var startTime = candidateSlots[0].StartTime;

                foreach (var classroom in classrooms)
                {
                    var newBlocks = candidateSlots.Select(ts => new RuntimeBlock(
                        Day: (int)ts.DayOfWeek,
                        StartTime: ts.StartTime,
                        EndTime: ts.EndTime,
                        ClassroomId: classroom.Id,
                        ClassroomName: classroom.Name,
                        ClassroomType: section.PrimaryClassroomType,
                        ClassroomCapacity: classroom.Capacity)).ToList();

                    var movedSection = CloneSectionWithNewSchedule(section, newBlocks, classroom.Id, classroom.Name, classroom.Capacity);

                    var hardConflicts = await _ruleEngine.EvaluateCandidateAsync(
                        movedSection, filteredSnapshot, config, state, ct);
                    var softPenalty = await _ruleEngine.CalculateCandidatePenaltyAsync(
                        movedSection, filteredSnapshot, config, state, ct);

                    if (hardConflicts.Count <= currentHard)
                    {
                        var improvement = currentPenalty - softPenalty;
                        suggestions.Add(new SuggestionItem(
                            0,
                            (int)day,
                            startTime.ToString("HH:mm"),
                            classroom.Id,
                            classroom.Name,
                            hardConflicts.Count,
                            Math.Round(softPenalty, 1),
                            Math.Round(improvement, 1),
                            hardConflicts.Take(3).Select(c => c.Message).ToList()));
                    }
                }
            }
        }

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
        var (snapshot, config, state) = await LoadEvaluationContextAsync(request.TermId, ct);

        // BEFORE
        var beforeBreakdown = await _ruleEngine.EvaluateWithBreakdownAsync(snapshot, config, state, ct);
        var before = new SimulationBreakdown(beforeBreakdown.TotalHardViolations, beforeBreakdown.TotalSoftPenalty);

        // Apply virtual changes to constraint definitions (in-memory only).
        // ConstraintState.Definitions mutable — bu blok V1'den port edildi; Plan 13'te
        // ConstraintState immutable record'a çevrilince `state with { ... }` haline gelir.
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
                    def.Weight = Convert.ToInt32(change.Value);
                    break;
                case "parameters":
                    def.Parameters = change.Value?.ToString();
                    break;
            }
        }

        // AFTER (mutated state)
        var afterBreakdown = await _ruleEngine.EvaluateWithBreakdownAsync(snapshot, config, state, ct);
        var after = new SimulationBreakdown(afterBreakdown.TotalHardViolations, afterBreakdown.TotalSoftPenalty);

        return BuildSimulationResult(before, after, beforeBreakdown, afterBreakdown);
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private async Task<(RuntimeSnapshot Snapshot, SchedulingConstraintConfig Config, ConstraintState State)>
        LoadEvaluationContextAsync(int termId, CancellationToken ct)
    {
        var snapshot = await _schedulingService.BuildRuntimeSnapshotAsync(termId, null, null, ct);
        var softConfig = await _schedulingService.LoadSoftConstraintConfigAsync(ct);
        var hardConfig = await _schedulingService.LoadHardConstraintConfigAsync(ct);

        var config = _schedulingService.BuildConstraintConfig(softConfig, hardConfig);
        var state = await _constraintStateLoader.LoadConstraintStateAsync(null, null, null, termId, ct);

        return (snapshot, config, state);
    }

    private static RuntimeSnapshot BuildFilteredSnapshot(RuntimeSnapshot snapshot, int excludeSectionId)
    {
        return new RuntimeSnapshot
        {
            TermId = snapshot.TermId,
            TermName = snapshot.TermName,
            ScopeName = snapshot.ScopeName,
            Sections = snapshot.Sections.Where(s => s.SectionId != excludeSectionId).ToList(),
            ClassroomAssignments = snapshot.ClassroomAssignments,
            DeptFacultyMap = snapshot.DeptFacultyMap,
            Corequisites = snapshot.Corequisites,
            SlotAllowedModes = snapshot.SlotAllowedModes,
            CohortConflictPairs = snapshot.CohortConflictPairs,
            CurriculumConflictMode = snapshot.CurriculumConflictMode,
            EnrollmentConflicts = snapshot.EnrollmentConflicts,
            CoEnrollmentPairs = snapshot.CoEnrollmentPairs,
            AdjacentYearSoftPairs = snapshot.AdjacentYearSoftPairs,
            DistributionConstraints = snapshot.DistributionConstraints,
        };
    }

    private static RuntimeSnapshot CloneSnapshotWithMove(RuntimeSnapshot original, SimulateMoveRequest move)
    {
        var newDay = move.NewDayOfWeek;
        var newStart = TimeOnly.Parse(move.NewTimeSlotStart);

        var sections = original.Sections.Select(s =>
        {
            if (s.SectionId != move.SectionId) return s;

            var duration = s.Blocks.Count > 0
                ? s.Blocks[0].EndTime - s.Blocks[0].StartTime
                : TimeSpan.FromMinutes(50);

            var blockCount = s.Blocks.Count > 0 ? s.Blocks.Count : Math.Max(s.TheoryHours + s.LabHours, 1);
            var classroomId = move.NewClassroomId ?? s.PrimaryClassroomId;
            var classroomName = classroomId != s.PrimaryClassroomId ? $"Derslik #{classroomId}" : s.PrimaryClassroomName;

            var newBlocks = new List<RuntimeBlock>();
            var currentStart = newStart;
            for (var i = 0; i < blockCount; i++)
            {
                newBlocks.Add(new RuntimeBlock(
                    Day: newDay,
                    StartTime: currentStart,
                    EndTime: currentStart.Add(duration),
                    ClassroomId: classroomId,
                    ClassroomName: classroomName,
                    ClassroomType: s.PrimaryClassroomType,
                    ClassroomCapacity: s.PrimaryClassroomCapacity));
                // Next block starts after a 10-minute break
                currentStart = currentStart.Add(duration).Add(TimeSpan.FromMinutes(10));
            }

            return s with
            {
                PrimaryClassroomId = classroomId,
                PrimaryClassroomName = classroomName,
                Blocks = newBlocks,
            };
        }).ToList();

        return new RuntimeSnapshot
        {
            TermId = original.TermId,
            TermName = original.TermName,
            ScopeName = original.ScopeName,
            Sections = sections,
            ClassroomAssignments = original.ClassroomAssignments,
            DeptFacultyMap = original.DeptFacultyMap,
            Corequisites = original.Corequisites,
            SlotAllowedModes = original.SlotAllowedModes,
            CohortConflictPairs = original.CohortConflictPairs,
            CurriculumConflictMode = original.CurriculumConflictMode,
            EnrollmentConflicts = original.EnrollmentConflicts,
            CoEnrollmentPairs = original.CoEnrollmentPairs,
            AdjacentYearSoftPairs = original.AdjacentYearSoftPairs,
            DistributionConstraints = original.DistributionConstraints,
        };
    }

    private static RuntimeCandidate CloneSectionWithNewSchedule(
        RuntimeCandidate original,
        List<RuntimeBlock> newBlocks,
        int classroomId,
        string classroomName,
        int classroomCapacity)
    {
        return original with
        {
            PrimaryClassroomId = classroomId,
            PrimaryClassroomName = classroomName,
            PrimaryClassroomCapacity = classroomCapacity,
            Blocks = newBlocks,
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
