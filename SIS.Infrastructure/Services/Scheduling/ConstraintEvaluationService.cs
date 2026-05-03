using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class ConstraintEvaluationService
{
    private const double SoftPenaltyPerConvertedConflict = 10;

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
            }

            var effectiveConfig = BuildEffectiveConfig(
                config, evaluator, state, candidate.InstructorId, candidate.DepartmentId, candidate.SectionId);

            var context = new SchedulingConstraintContext
            {
                Candidate = candidate,
                Snapshot = snapshot,
                Config = effectiveConfig,
            };

            var result = evaluator.Evaluate(context);
            var effectiveCategory = ResolveCategory(evaluator, state);
            var categoryChanged = effectiveCategory != evaluator.Category;
            AppendCandidateResult(
                result, evaluator, effectiveCategory, categoryChanged, candidate,
                allConflicts, allNotes, ref totalPenalty);
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
                CohortConflictPairs = snapshot.CohortConflictPairs,
                EnrollmentConflicts = snapshot.EnrollmentConflicts,
                DistributionConstraints = snapshot.DistributionConstraints,
                SlotAllowedModes = snapshot.SlotAllowedModes,
            };

            foreach (var evaluator in _evaluators)
            {
                if (state != null)
                {
                    if (state.IsDisabled(evaluator.Code))
                        continue;

                    if (state.IsExempt(evaluator.Code, section.InstructorId, section.DepartmentId, section.SectionId))
                        continue;
                }

                var effectiveConfig = BuildEffectiveConfig(
                    config, evaluator, state, section.InstructorId, section.DepartmentId, section.SectionId);

                var context = new SchedulingConstraintContext
                {
                    Candidate = section,
                    Snapshot = filteredSnapshot,
                    Config = effectiveConfig,
                };

                var result = evaluator.Evaluate(context);
                var effectiveCategory = ResolveCategory(evaluator, state);
                var categoryChanged = effectiveCategory != evaluator.Category;
                var normalizedConflicts = new List<SchedulingConflictDto>();
                double ignoredPenalty = 0;
                AppendCandidateResult(
                    result, evaluator, effectiveCategory, categoryChanged, section,
                    normalizedConflicts, allNotes, ref ignoredPenalty);

                foreach (var conflict in normalizedConflicts)
                {
                    // Deduplicate bidirectional conflicts (A conflicts with B = B conflicts with A)
                    var key = BuildConflictKey(conflict);
                    if (seen.Add(key))
                        allConflicts.Add(conflict);
                }
            }
        }

        return (allConflicts, allNotes);
    }

    /// <summary>
    /// Evaluate all sections and return per-constraint score breakdown.
    /// </summary>
    public ScoreBreakdownDto EvaluateWithBreakdown(
        TermSnapshot snapshot, SchedulingConstraintConfig config,
        ConstraintState state, Dictionary<string, int> weightMap)
    {
        var perConstraint = new Dictionary<string, (List<SchedulingConflictDto> Conflicts, List<string> Notes, double Penalty)>();

        foreach (var section in snapshot.Sections)
        {
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
                CohortConflictPairs = snapshot.CohortConflictPairs,
                EnrollmentConflicts = snapshot.EnrollmentConflicts,
                DistributionConstraints = snapshot.DistributionConstraints,
                SlotAllowedModes = snapshot.SlotAllowedModes,
            };

            foreach (var evaluator in _evaluators)
            {
                if (state.IsDisabled(evaluator.Code)) continue;
                if (state.IsExempt(evaluator.Code, section.InstructorId, section.DepartmentId, section.SectionId)) continue;

                var effectiveConfig = BuildEffectiveConfig(
                    config, evaluator, state, section.InstructorId, section.DepartmentId, section.SectionId);

                var context = new SchedulingConstraintContext
                {
                    Candidate = section,
                    Snapshot = filteredSnapshot,
                    Config = effectiveConfig,
                };

                var result = evaluator.Evaluate(context);
                var effectiveCategory = ResolveCategory(evaluator, state);
                var categoryChanged = effectiveCategory != evaluator.Category;
                var normalizedConflicts = new List<SchedulingConflictDto>();
                var normalizedNotes = new List<string>();
                double normalizedPenalty = 0;
                AppendCandidateResult(
                    result, evaluator, effectiveCategory, categoryChanged, section,
                    normalizedConflicts, normalizedNotes, ref normalizedPenalty);

                if (!perConstraint.ContainsKey(evaluator.Code))
                    perConstraint[evaluator.Code] = (new List<SchedulingConflictDto>(), new List<string>(), 0);

                var entry = perConstraint[evaluator.Code];
                entry.Conflicts.AddRange(normalizedConflicts);
                entry.Notes.AddRange(normalizedNotes);
                perConstraint[evaluator.Code] = (entry.Conflicts, entry.Notes, entry.Penalty + normalizedPenalty);
            }
        }

        var items = new List<ConstraintScoreItemDto>();
        int totalHard = 0;
        double totalSoft = 0;

        foreach (var evaluator in _evaluators)
        {
            if (!perConstraint.TryGetValue(evaluator.Code, out var data)) continue;

            var weight = weightMap.GetValueOrDefault(evaluator.Code, 5);
            var effectiveCategory = ResolveCategory(evaluator, state);
            var seen = new HashSet<string>();
            var deduped = data.Conflicts.Where(c => seen.Add(BuildConflictKey(c))).ToList();

            var rawPenalty = data.Penalty;
            var weightedPenalty = effectiveCategory == ConstraintCategory.Soft ? rawPenalty * weight : 0;

            if (effectiveCategory == ConstraintCategory.Hard)
                totalHard += deduped.Count;
            else
                totalSoft += weightedPenalty;

            if (deduped.Count > 0 || rawPenalty > 0)
            {
                items.Add(new ConstraintScoreItemDto(
                    evaluator.Code, evaluator.Name,
                    effectiveCategory.ToString(), weight,
                    deduped.Count, Math.Round(rawPenalty, 1), Math.Round(weightedPenalty, 1), 0,
                    deduped.Select(c => c.SectionId).Distinct().Count(),
                    deduped.Take(5).Select(c => c.Message).ToList()));
            }
        }

        var totalValue = totalHard + totalSoft;
        if (totalValue > 0)
        {
            items = items.Select(i => i with
            {
                Percentage = Math.Round(
                    (i.Category == "Hard" ? i.ViolationCount : i.WeightedPenalty) / totalValue * 100, 1)
            }).ToList();
        }

        items = items.OrderByDescending(i => i.Category == "Hard" ? i.ViolationCount * 1000 : i.WeightedPenalty).ToList();

        return new ScoreBreakdownDto(totalHard, Math.Round(totalSoft, 1), totalHard == 0, items);
    }

    /// <summary>
    /// Calculate soft penalty score only (for auto-scheduling scoring).
    /// SA/LNS optimizer'ı bu fonksiyonu çağırıyor — admin'in kısıt ağırlıkları (1-10)
    /// burada SoftPenalty ile çarpılarak motora aktarılıyor.
    /// </summary>
    public double CalculatePenalty(
        SectionSnapshot candidate, TermSnapshot snapshot, SchedulingConstraintConfig config,
        ConstraintState? state = null)
    {
        double totalPenalty = 0;
        foreach (var evaluator in _evaluators)
        {
            var effectiveCategory = ResolveCategory(evaluator, state);
            if (effectiveCategory != ConstraintCategory.Soft)
                continue;

            if (state != null)
            {
                if (state.IsDisabled(evaluator.Code))
                    continue;

                if (state.IsExempt(evaluator.Code, candidate.InstructorId, candidate.DepartmentId, candidate.SectionId))
                    continue;
            }

            var effectiveConfig = BuildEffectiveConfig(
                config, evaluator, state, candidate.InstructorId, candidate.DepartmentId, candidate.SectionId);

            var context = new SchedulingConstraintContext
            {
                Candidate = candidate,
                Snapshot = snapshot,
                Config = effectiveConfig,
            };

            var result = evaluator.Evaluate(context);
            var categoryChanged = effectiveCategory != evaluator.Category;
            var weight = ResolveWeight(evaluator, state);
            totalPenalty += result.SoftPenalty * weight;
            if (categoryChanged && result.Conflicts.Count > 0)
                totalPenalty += ConvertConflictsToSoftPenalty(result.Conflicts.Count) * weight;
        }
        return totalPenalty;
    }

    private static ConstraintCategory ResolveCategory(IConstraintEvaluator evaluator, ConstraintState? state)
    {
        if (state?.Definitions.TryGetValue(evaluator.Code, out var definition) == true
            && Enum.TryParse<ConstraintCategory>(definition.Category, ignoreCase: true, out var category))
        {
            return category;
        }

        return evaluator.Category;
    }

    /// <summary>
    /// Admin kısıt kartından ayarlanan 1-10 ağırlık. State yoksa veya tanım eksikse 1 dön (nötr çarpan).
    /// </summary>
    private static int ResolveWeight(IConstraintEvaluator evaluator, ConstraintState? state)
    {
        if (state?.Definitions.TryGetValue(evaluator.Code, out var definition) == true)
        {
            return Math.Clamp(definition.Weight, 1, 10);
        }
        return 1;
    }

    /// <summary>
    /// Override sıralaması: baseline (FromSystemConfig) → ConstraintDefinition.Parameters
    /// (kısıt kartından admin ayarı) → ConstraintException.ParameterOverride (entity-spesifik istisna).
    /// En spesifik kazanır. State yoksa baseline döner.
    /// </summary>
    private SchedulingConstraintConfig BuildEffectiveConfig(
        SchedulingConstraintConfig baseConfig,
        IConstraintEvaluator evaluator,
        ConstraintState? state,
        int? instructorId, int? departmentId, int? sectionId)
    {
        if (state == null) return baseConfig;

        var current = baseConfig;

        // 1. Kısıt kartı parametreleri (genel admin ayarı)
        if (state.Definitions.TryGetValue(evaluator.Code, out var def)
            && !string.IsNullOrWhiteSpace(def.Parameters))
        {
            current = MergeParameterOverride(current, def.Parameters);
        }

        // 2. İstisna override'ı (entity-spesifik, en yerel)
        var exOverride = state.GetParameterOverride(evaluator.Code, instructorId, departmentId, sectionId);
        if (exOverride != null)
        {
            current = MergeParameterOverride(current, exOverride);
        }

        return current;
    }

    private static void AppendCandidateResult(
        ConstraintResult result,
        IConstraintEvaluator evaluator,
        ConstraintCategory effectiveCategory,
        bool categoryChanged,
        SectionSnapshot candidate,
        List<SchedulingConflictDto> conflicts,
        List<string> notes,
        ref double softPenalty)
    {
        notes.AddRange(result.Notes);

        if (effectiveCategory == ConstraintCategory.Hard)
        {
            conflicts.AddRange(result.Conflicts);

            if (categoryChanged && result.Conflicts.Count == 0 && result.SoftPenalty > 0)
                conflicts.Add(BuildSyntheticHardConflict(candidate, evaluator, result));

            return;
        }

        softPenalty += result.SoftPenalty;

        if (categoryChanged && result.Conflicts.Count > 0)
        {
            softPenalty += ConvertConflictsToSoftPenalty(result.Conflicts.Count);
            notes.AddRange(result.Conflicts.Select(c => c.Message));
            return;
        }

        // Some soft evaluators can still emit real hard conflicts from their own domain
        // model (for example DIST with PreferenceLevel.Unavailable). Preserve those
        // unless the DB category explicitly moved the evaluator from Hard to Soft.
        conflicts.AddRange(result.Conflicts);
    }

    private static double ConvertConflictsToSoftPenalty(int conflictCount) =>
        conflictCount * SoftPenaltyPerConvertedConflict;

    private static SchedulingConflictDto BuildSyntheticHardConflict(
        SectionSnapshot candidate,
        IConstraintEvaluator evaluator,
        ConstraintResult result)
    {
        var firstSlot = candidate.TimeBlocks
            .OrderBy(block => block.Day)
            .ThenBy(block => block.StartTime)
            .FirstOrDefault();

        var detail = result.Notes.FirstOrDefault()
            ?? $"Soft penalty {result.SoftPenalty:0.##}";

        return SchedulingHelpers.BuildConflict(
            candidate,
            evaluator.Code,
            $"{evaluator.Code}: {evaluator.Name} hard constraint violation ({detail})",
            firstSlot,
            relatedSectionId: null);
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
