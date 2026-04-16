using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class ConstraintEvaluationService
{
    private readonly IEnumerable<IConstraintEvaluator> _evaluators;
    private readonly AppDbContext _context;
    private readonly ILogger<ConstraintEvaluationService> _logger;

    public ConstraintEvaluationService(
        IEnumerable<IConstraintEvaluator> evaluators,
        AppDbContext context,
        ILogger<ConstraintEvaluationService> logger)
    {
        _evaluators = evaluators;
        _context = context;
        _logger = logger;
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
    /// Merges a JSON parameter override into the existing config using reflection.
    /// Unknown keys are logged as warnings. Conversion errors are logged, not silenced.
    /// </summary>
    private SchedulingConstraintConfig MergeParameterOverride(SchedulingConstraintConfig baseConfig, string parameterOverrideJson)
    {
        try
        {
            var overrides = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(parameterOverrideJson);
            if (overrides == null || overrides.Count == 0)
                return baseConfig;

            var props = typeof(SchedulingConstraintConfig)
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            var values = props.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.GetValue(baseConfig),
                StringComparer.OrdinalIgnoreCase);

            foreach (var (key, jsonVal) in overrides)
            {
                if (!props.TryGetValue(key, out var prop))
                {
                    _logger.LogWarning("MergeParameterOverride: unknown key '{Key}' ignored", key);
                    continue;
                }

                try
                {
                    var converted = ConvertJsonElement(jsonVal, prop.PropertyType);
                    if (converted != null)
                        values[prop.Name] = converted;
                    else
                        _logger.LogWarning("MergeParameterOverride: could not convert '{Key}' value to {Type}", key, prop.PropertyType.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "MergeParameterOverride: error converting '{Key}'", key);
                }
            }

            var merged = new SchedulingConstraintConfig();
            foreach (var (name, prop) in props)
            {
                if (values.TryGetValue(name, out var val))
                    prop.SetValue(merged, val);
            }

            return merged;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MergeParameterOverride: failed to parse JSON, returning base config");
            return baseConfig;
        }
    }

    private static object? ConvertJsonElement(System.Text.Json.JsonElement el, Type targetType)
    {
        if (targetType == typeof(int) && el.ValueKind == System.Text.Json.JsonValueKind.Number)
            return el.GetInt32();
        if (targetType == typeof(double) && el.ValueKind == System.Text.Json.JsonValueKind.Number)
            return el.GetDouble();
        if (targetType == typeof(bool) && (el.ValueKind == System.Text.Json.JsonValueKind.True || el.ValueKind == System.Text.Json.JsonValueKind.False))
            return el.GetBoolean();
        if (targetType == typeof(string) && el.ValueKind == System.Text.Json.JsonValueKind.String)
            return el.GetString();
        if (targetType == typeof(TimeOnly) && el.ValueKind == System.Text.Json.JsonValueKind.String)
            return TimeOnly.TryParse(el.GetString(), out var t) ? t : null;
        return null;
    }

    private static string BuildConflictKey(SchedulingConflictDto c)
    {
        var ids = new[] { c.SectionId, c.RelatedSectionId ?? 0 };
        Array.Sort(ids);
        return $"{c.ConflictType}:{ids[0]}:{ids[1]}:{c.Day}:{c.StartTime}";
    }
}
