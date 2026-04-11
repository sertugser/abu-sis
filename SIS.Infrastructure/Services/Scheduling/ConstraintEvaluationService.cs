using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

internal sealed class ConstraintEvaluationService
{
    private readonly IEnumerable<IConstraintEvaluator> _evaluators;
    private readonly AppDbContext _context;

    public ConstraintEvaluationService(IEnumerable<IConstraintEvaluator> evaluators, AppDbContext context)
    {
        _evaluators = evaluators;
        _context = context;
    }

    /// <summary>
    /// Load constraint definitions and applicable exceptions from the database.
    /// Call once per evaluation batch, then pass the state to evaluation methods.
    /// </summary>
    public async Task<ConstraintState> LoadConstraintStateAsync(
        int? instructorId, int? departmentId, int? sectionId, int? termId,
        CancellationToken ct)
    {
        var definitions = await _context.ConstraintDefinitions
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Code, ct);

        var exceptions = await _context.ConstraintExceptions
            .AsNoTracking()
            .Include(e => e.ConstraintDefinition)
            .Where(e =>
                (e.InstructorId == null || e.InstructorId == instructorId) &&
                (e.DepartmentId == null || e.DepartmentId == departmentId) &&
                (e.CourseSectionId == null || e.CourseSectionId == sectionId) &&
                (e.TermId == null || e.TermId == termId))
            .ToListAsync(ct);

        return new ConstraintState(definitions, exceptions);
    }

    /// <summary>
    /// Evaluate a single candidate section against term snapshot.
    /// </summary>
    public (List<SchedulingConflictDto> Conflicts, List<string> Notes, double SoftPenalty) EvaluateCandidate(
        SectionSnapshot candidate, TermSnapshot snapshot, SchedulingConstraintConfig config,
        ConstraintState? state = null)
    {
        var context = new SchedulingConstraintContext
        {
            Candidate = candidate,
            Snapshot = snapshot,
            Config = config,
        };

        var allConflicts = new List<SchedulingConflictDto>();
        var allNotes = new List<string>();
        double totalPenalty = 0;

        foreach (var evaluator in _evaluators)
        {
            if (state != null)
            {
                // B25: Check if constraint is disabled
                if (state.IsDisabled(evaluator.Code))
                    continue;

                // B25: Check if there's an exemption for this candidate
                if (state.IsExempt(evaluator.Code, candidate.InstructorId, candidate.DepartmentId, candidate.SectionId))
                    continue;

                // B27: Check for parameter override and apply to config
                var paramOverride = state.GetParameterOverride(evaluator.Code, candidate.InstructorId, candidate.DepartmentId, candidate.SectionId);
                if (paramOverride != null)
                {
                    context = new SchedulingConstraintContext
                    {
                        Candidate = context.Candidate,
                        Snapshot = context.Snapshot,
                        Config = MergeParameterOverride(config, paramOverride),
                    };
                }
            }

            var result = evaluator.Evaluate(context);
            allConflicts.AddRange(result.Conflicts);
            allNotes.AddRange(result.Notes);
            totalPenalty += result.SoftPenalty;
        }

        return (allConflicts, allNotes, totalPenalty);
    }

    /// <summary>
    /// Evaluate all existing sections in a term snapshot (for validate-existing).
    /// </summary>
    public (List<SchedulingConflictDto> Conflicts, List<string> Notes) EvaluateAllExisting(
        TermSnapshot snapshot, SchedulingConstraintConfig config,
        ConstraintState? state = null)
    {
        var allConflicts = new List<SchedulingConflictDto>();
        var allNotes = new List<string>();
        var seen = new HashSet<string>(); // dedup bidirectional conflicts

        foreach (var section in snapshot.Sections)
        {
            // Build a snapshot that excludes the current section (evaluators compare candidate vs snapshot)
            var filteredSnapshot = new TermSnapshot
            {
                TermId = snapshot.TermId,
                TermName = snapshot.TermName,
                ScopeName = snapshot.ScopeName,
                Sections = snapshot.Sections.Where(s => s.SectionId != section.SectionId).ToList(),
                ClassroomAssignments = snapshot.ClassroomAssignments,
                DeptFacultyMap = snapshot.DeptFacultyMap,
                Corequisites = snapshot.Corequisites,
                CoEnrollmentPairs = snapshot.CoEnrollmentPairs,
                CourseSuggestedSemestersByDepartment = snapshot.CourseSuggestedSemestersByDepartment,
                CourseAccessibleDepartments = snapshot.CourseAccessibleDepartments,
            };

            var context = new SchedulingConstraintContext
            {
                Candidate = section,
                Snapshot = filteredSnapshot,
                Config = config,
            };

            foreach (var evaluator in _evaluators)
            {
                if (state != null)
                {
                    if (state.IsDisabled(evaluator.Code))
                        continue;

                    if (state.IsExempt(evaluator.Code, section.InstructorId, section.DepartmentId, section.SectionId))
                        continue;

                    var paramOverride = state.GetParameterOverride(evaluator.Code, section.InstructorId, section.DepartmentId, section.SectionId);
                    if (paramOverride != null)
                    {
                        context = new SchedulingConstraintContext
                        {
                            Candidate = context.Candidate,
                            Snapshot = context.Snapshot,
                            Config = MergeParameterOverride(config, paramOverride),
                        };
                    }
                }

                var result = evaluator.Evaluate(context);
                foreach (var conflict in result.Conflicts)
                {
                    // Deduplicate bidirectional conflicts (A conflicts with B = B conflicts with A)
                    var key = BuildConflictKey(conflict);
                    if (seen.Add(key))
                        allConflicts.Add(conflict);
                }
                allNotes.AddRange(result.Notes);
            }
        }

        return (allConflicts, allNotes);
    }

    /// <summary>
    /// Calculate soft penalty score only (for auto-scheduling scoring).
    /// </summary>
    public double CalculatePenalty(
        SectionSnapshot candidate, TermSnapshot snapshot, SchedulingConstraintConfig config,
        ConstraintState? state = null)
    {
        var context = new SchedulingConstraintContext
        {
            Candidate = candidate,
            Snapshot = snapshot,
            Config = config,
        };

        double totalPenalty = 0;
        foreach (var evaluator in _evaluators.Where(e => e.Category == ConstraintCategory.Soft))
        {
            if (state != null)
            {
                if (state.IsDisabled(evaluator.Code))
                    continue;

                if (state.IsExempt(evaluator.Code, candidate.InstructorId, candidate.DepartmentId, candidate.SectionId))
                    continue;

                var paramOverride = state.GetParameterOverride(evaluator.Code, candidate.InstructorId, candidate.DepartmentId, candidate.SectionId);
                if (paramOverride != null)
                {
                    context = new SchedulingConstraintContext
                    {
                        Candidate = context.Candidate,
                        Snapshot = context.Snapshot,
                        Config = MergeParameterOverride(config, paramOverride),
                    };
                }
            }

            totalPenalty += evaluator.Evaluate(context).SoftPenalty;
        }
        return totalPenalty;
    }

    /// <summary>
    /// Merges a JSON parameter override into the existing config.
    /// Only known keys are applied; unknown keys are silently ignored.
    /// </summary>
    private static SchedulingConstraintConfig MergeParameterOverride(SchedulingConstraintConfig baseConfig, string parameterOverrideJson)
    {
        try
        {
            var overrides = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(parameterOverrideJson);
            if (overrides == null) return baseConfig;

            // Create a mutable copy by reconstructing from existing values
            var merged = new SchedulingConstraintConfig
            {
                LunchBreakIsHard = overrides.TryGetValue("LunchBreakIsHard", out var v1) && (v1.ValueKind == System.Text.Json.JsonValueKind.True || v1.ValueKind == System.Text.Json.JsonValueKind.False)
                    ? v1.GetBoolean() : baseConfig.LunchBreakIsHard,
                LunchBreakStart = baseConfig.LunchBreakStart,
                LunchBreakEnd = baseConfig.LunchBreakEnd,
                MaxConsecutiveHours = overrides.TryGetValue("MaxConsecutiveHours", out var v2) && v2.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? v2.GetInt32() : baseConfig.MaxConsecutiveHours,
                BreakDurationMinutes = overrides.TryGetValue("BreakDurationMinutes", out var v3) && v3.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? v3.GetInt32() : baseConfig.BreakDurationMinutes,
                AmphiThreshold = overrides.TryGetValue("AmphiThreshold", out var v4) && v4.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? v4.GetInt32() : baseConfig.AmphiThreshold,
                S1Enabled = baseConfig.S1Enabled,
                S1MinOccupancy = overrides.TryGetValue("S1MinOccupancy", out var v5) && v5.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? v5.GetInt32() : baseConfig.S1MinOccupancy,
                S1MaxOccupancy = overrides.TryGetValue("S1MaxOccupancy", out var v6) && v6.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? v6.GetInt32() : baseConfig.S1MaxOccupancy,
                S2Enabled = baseConfig.S2Enabled,
                S2MaxDailyHours = overrides.TryGetValue("S2MaxDailyHours", out var v7) && v7.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? v7.GetInt32() : baseConfig.S2MaxDailyHours,
                S3Enabled = baseConfig.S3Enabled,
                S3MaxSectionsPerSlot = overrides.TryGetValue("S3MaxSectionsPerSlot", out var v8) && v8.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? v8.GetInt32() : baseConfig.S3MaxSectionsPerSlot,
                S4Enabled = baseConfig.S4Enabled,
                S4MinCoPairThreshold = overrides.TryGetValue("S4MinCoPairThreshold", out var v9) && v9.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? v9.GetInt32() : baseConfig.S4MinCoPairThreshold,
                S5Enabled = baseConfig.S5Enabled,
                S7Enabled = baseConfig.S7Enabled,
                S7MaxGapSlots = overrides.TryGetValue("S7MaxGapSlots", out var v10) && v10.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? v10.GetInt32() : baseConfig.S7MaxGapSlots,
                S8Enabled = baseConfig.S8Enabled,
                S9Enabled = baseConfig.S9Enabled,
                S9MorningStart = baseConfig.S9MorningStart,
                S9EveningStart = baseConfig.S9EveningStart,
                S12Enabled = baseConfig.S12Enabled,
                S12MaxImbalancePercent = overrides.TryGetValue("S12MaxImbalancePercent", out var v11) && v11.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? v11.GetInt32() : baseConfig.S12MaxImbalancePercent,
            };

            return merged;
        }
        catch
        {
            return baseConfig;
        }
    }

    private static string BuildConflictKey(SchedulingConflictDto c)
    {
        var ids = new[] { c.SectionId, c.RelatedSectionId ?? 0 };
        Array.Sort(ids);
        return $"{c.ConflictType}:{ids[0]}:{ids[1]}:{c.Day}:{c.StartTime}";
    }
}
