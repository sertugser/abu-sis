using Microsoft.Extensions.Logging;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules;

/// <summary>
/// Plan 11 + Plan 12 — IRulePredicate listesi üzerinde Plan 10 disabled/exempt/category
/// mantığını uygular. Hard predicate'lar <see cref="IRulePredicate.Evaluate"/> ile violation
/// üretir; soft predicate'lar (<see cref="ISoftRulePredicate"/>) <c>CalculatePenalty</c> ile
/// penalty üretir. Plan 10 Category toggle DB'de Hard ↔ Soft dönüşümü yapar — RuleEngine
/// effective category'ye göre uygun method'u çağırır.
/// </summary>
public sealed class RuleEngine : IRuleEngine
{
    private readonly IEnumerable<IRulePredicate> _predicates;
    private readonly ILogger<RuleEngine> _logger;

    public RuleEngine(IEnumerable<IRulePredicate> predicates, ILogger<RuleEngine> logger)
    {
        _predicates = predicates;
        _logger = logger;
    }

    public Task<IReadOnlyList<SchedulingConflictDto>> EvaluateCandidateAsync(
        RuntimeCandidate candidate,
        RuntimeSnapshot snapshot,
        SchedulingConstraintConfig config,
        ConstraintState? state,
        CancellationToken ct)
    {
        var conflicts = RunHardPredicates(candidate, snapshot, config, state);
        return Task.FromResult<IReadOnlyList<SchedulingConflictDto>>(conflicts);
    }

    public Task<IReadOnlyList<SchedulingConflictDto>> EvaluateAllExistingAsync(
        RuntimeSnapshot snapshot,
        SchedulingConstraintConfig config,
        ConstraintState? state,
        CancellationToken ct)
    {
        var seen = new HashSet<string>();
        var all = new List<SchedulingConflictDto>();

        foreach (var section in snapshot.Sections)
        {
            var filtered = FilterSectionOut(snapshot, section.SectionId);
            var perSection = RunHardPredicates(section, filtered, config, state);
            foreach (var c in perSection)
            {
                var key = BuildDedupKey(c);
                if (seen.Add(key))
                    all.Add(c);
            }
        }

        return Task.FromResult<IReadOnlyList<SchedulingConflictDto>>(all);
    }

    /// <summary>
    /// Plan 12 Faz 4 — Tüm snapshot için score breakdown.
    /// Per-section hard violations + per-predicate soft penalty toplamı + per-constraint
    /// breakdown kalemleri üretir. V1 <c>ConstraintEvaluationService.EvaluateWithBreakdown</c>
    /// fonksiyonel eşdeğeri.
    /// </summary>
    public Task<ScoreBreakdownDto> EvaluateWithBreakdownAsync(
        RuntimeSnapshot snapshot,
        SchedulingConstraintConfig config,
        ConstraintState? state,
        CancellationToken ct)
    {
        var hardSeen = new HashSet<string>();
        var totalHardViolations = 0;
        // Per-predicate breakdown bookkeeping.
        var hardCounts = new Dictionary<string, (int Count, List<string> Messages, HashSet<int> Sections)>();
        var softTotals = new Dictionary<string, (double RawPenalty, int ViolationCount, HashSet<int> Sections, List<string> Messages)>();

        foreach (var section in snapshot.Sections)
        {
            var filtered = FilterSectionOut(snapshot, section.SectionId);
            var ctx = new RuleContext
            {
                Candidate = section, Snapshot = filtered, Config = config, State = state,
            };

            foreach (var predicate in _predicates)
            {
                if (state != null)
                {
                    if (state.IsDisabled(predicate.Code)) continue;
                    if (state.IsExempt(predicate.Code, section.InstructorId, section.DepartmentId, section.SectionId)) continue;
                }

                var effectiveCategory = ResolveCategory(predicate, state);

                if (effectiveCategory == RuleCategory.Hard)
                {
                    IReadOnlyList<RuleViolation> violations;
                    try { violations = predicate.Evaluate(ctx); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "RuleEngine.Breakdown: hard predicate {Code} threw for section {SectionId}",
                            predicate.Code, section.SectionId);
                        continue;
                    }

                    foreach (var v in violations)
                    {
                        var dto = MapToConflictDto(v);
                        var key = BuildDedupKey(dto);
                        if (!hardSeen.Add(key)) continue;
                        totalHardViolations++;
                        if (!hardCounts.TryGetValue(predicate.Code, out var entry))
                            entry = (0, new List<string>(), new HashSet<int>());
                        entry.Count++;
                        entry.Sections.Add(section.SectionId);
                        if (entry.Messages.Count < 3) entry.Messages.Add(v.Message);
                        hardCounts[predicate.Code] = entry;
                    }
                }
                else if (effectiveCategory == RuleCategory.Soft)
                {
                    if (predicate is not ISoftRulePredicate softPredicate)
                    {
                        // Hybrid değil saf hard predicate Soft'a çekilmiş → CalculatePenalty yok, skip.
                        continue;
                    }

                    SoftRuleResult result;
                    try { result = softPredicate.CalculatePenalty(ctx); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "RuleEngine.Breakdown: soft predicate {Code} threw for section {SectionId}",
                            predicate.Code, section.SectionId);
                        continue;
                    }

                    if (result.RawPenalty <= 0) continue;

                    if (!softTotals.TryGetValue(predicate.Code, out var entry))
                        entry = (0, 0, new HashSet<int>(), new List<string>());
                    entry.RawPenalty += result.RawPenalty;
                    entry.ViolationCount += result.ViolationCount;
                    entry.Sections.Add(section.SectionId);
                    foreach (var msg in result.SampleMessages)
                        if (entry.Messages.Count < 5) entry.Messages.Add(msg);
                    softTotals[predicate.Code] = entry;
                }
            }
        }

        // Aggregate ConstraintScoreItem listesi
        var items = new List<ConstraintScoreItemDto>();
        double totalSoftPenalty = 0;

        // Tüm predicate'lar için satır üret (penalty 0 olsa bile breakdown UI tutarlılığı için).
        var allCodes = _predicates.Select(p => p.Code).Distinct().ToList();
        foreach (var code in allCodes)
        {
            var predicate = _predicates.First(p => p.Code == code);
            var effectiveCategory = ResolveCategory(predicate, state);
            var weight = state?.Definitions.TryGetValue(code, out var def) == true ? def.Weight : 1;
            var defName = state?.Definitions.TryGetValue(code, out var def2) == true ? def2.Name : code;

            if (effectiveCategory == RuleCategory.Hard && hardCounts.TryGetValue(code, out var hardEntry))
            {
                items.Add(new ConstraintScoreItemDto(
                    Code: code, Name: defName, Category: "Hard", Weight: weight,
                    ViolationCount: hardEntry.Count, RawPenalty: 0, WeightedPenalty: 0,
                    Percentage: 0, AffectedSectionCount: hardEntry.Sections.Count,
                    SampleViolations: hardEntry.Messages.ToList()));
            }
            else if (effectiveCategory == RuleCategory.Soft && softTotals.TryGetValue(code, out var softEntry))
            {
                var weighted = softEntry.RawPenalty * weight;
                totalSoftPenalty += weighted;
                items.Add(new ConstraintScoreItemDto(
                    Code: code, Name: defName, Category: "Soft", Weight: weight,
                    ViolationCount: softEntry.ViolationCount, RawPenalty: softEntry.RawPenalty,
                    WeightedPenalty: weighted, Percentage: 0,
                    AffectedSectionCount: softEntry.Sections.Count,
                    SampleViolations: softEntry.Messages.ToList()));
            }
        }

        // Percentage hesabı: weighted penalty'nin total'a oranı.
        for (var i = 0; i < items.Count; i++)
        {
            if (totalSoftPenalty > 0 && items[i].Category == "Soft")
            {
                var pct = items[i].WeightedPenalty * 100.0 / totalSoftPenalty;
                items[i] = items[i] with { Percentage = pct };
            }
        }

        return Task.FromResult(new ScoreBreakdownDto(
            TotalHardViolations: totalHardViolations,
            TotalSoftPenalty: totalSoftPenalty,
            Feasible: totalHardViolations == 0,
            Constraints: items));
    }

    /// <summary>
    /// Plan 12 Faz 4 — Tek candidate için weighted soft penalty toplamı.
    /// V1 <c>ConstraintEvaluationService.CalculatePenalty</c> fonksiyonel eşdeğeri (SA/LNS path için).
    /// </summary>
    public Task<double> CalculateCandidatePenaltyAsync(
        RuntimeCandidate candidate,
        RuntimeSnapshot snapshot,
        SchedulingConstraintConfig config,
        ConstraintState? state,
        CancellationToken ct)
    {
        var ctx = new RuleContext
        {
            Candidate = candidate, Snapshot = snapshot, Config = config, State = state,
        };

        double total = 0;
        foreach (var predicate in _predicates)
        {
            if (state != null)
            {
                if (state.IsDisabled(predicate.Code)) continue;
                if (state.IsExempt(predicate.Code, candidate.InstructorId, candidate.DepartmentId, candidate.SectionId)) continue;
            }

            if (ResolveCategory(predicate, state) != RuleCategory.Soft) continue;
            if (predicate is not ISoftRulePredicate softPredicate) continue;

            SoftRuleResult result;
            try { result = softPredicate.CalculatePenalty(ctx); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RuleEngine.Penalty: soft predicate {Code} threw for section {SectionId}",
                    predicate.Code, candidate.SectionId);
                continue;
            }

            if (result.RawPenalty <= 0) continue;
            var weight = state?.Definitions.TryGetValue(predicate.Code, out var def) == true ? def.Weight : 1;
            total += result.RawPenalty * weight;
        }

        return Task.FromResult(total);
    }

    // Hard-only predicate dispatch (EvaluateCandidate / EvaluateAllExisting için).
    private List<SchedulingConflictDto> RunHardPredicates(
        RuntimeCandidate candidate, RuntimeSnapshot snapshot,
        SchedulingConstraintConfig config, ConstraintState? state)
    {
        var result = new List<SchedulingConflictDto>();
        var ctx = new RuleContext
        {
            Candidate = candidate, Snapshot = snapshot, Config = config, State = state,
        };

        foreach (var predicate in _predicates)
        {
            if (state != null)
            {
                if (state.IsDisabled(predicate.Code)) continue;
                if (state.IsExempt(predicate.Code, candidate.InstructorId, candidate.DepartmentId, candidate.SectionId)) continue;
            }

            if (ResolveCategory(predicate, state) != RuleCategory.Hard) continue;

            IReadOnlyList<RuleViolation> violations;
            try { violations = predicate.Evaluate(ctx); }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "RuleEngine: predicate {Code} threw for section {SectionId}. Skipping.",
                    predicate.Code, candidate.SectionId);
                continue;
            }

            foreach (var v in violations)
                result.Add(MapToConflictDto(v));
        }

        return result;
    }

    /// <summary>Plan 10 — DB'den Category override; varsayılan predicate.DefaultCategory.</summary>
    private static RuleCategory ResolveCategory(IRulePredicate predicate, ConstraintState? state)
    {
        if (state?.Definitions.TryGetValue(predicate.Code, out var def) == true
            && Enum.TryParse<RuleCategory>(def.Category, ignoreCase: true, out var category))
        {
            return category;
        }
        return predicate.DefaultCategory;
    }

    /// <summary>Snapshot'tan tek section'ı çıkar — EvaluateAllExisting filteredSnapshot deseni.</summary>
    private static RuntimeSnapshot FilterSectionOut(RuntimeSnapshot snapshot, int sectionIdToExclude)
    {
        return new RuntimeSnapshot
        {
            TermId = snapshot.TermId,
            TermName = snapshot.TermName,
            ScopeName = snapshot.ScopeName,
            Sections = snapshot.Sections.Where(s => s.SectionId != sectionIdToExclude).ToList(),
            ClassroomAssignments = snapshot.ClassroomAssignments,
            DeptFacultyMap = snapshot.DeptFacultyMap,
            Corequisites = snapshot.Corequisites,
            SlotAllowedModes = snapshot.SlotAllowedModes,
            CohortConflictPairs = snapshot.CohortConflictPairs,
            CurriculumConflictMode = snapshot.CurriculumConflictMode,
            EnrollmentConflicts = snapshot.EnrollmentConflicts,
            // Plan 12 Faz 4 — soft predicate alanları da kopyalanmalı.
            CoEnrollmentPairs = snapshot.CoEnrollmentPairs,
            AdjacentYearSoftPairs = snapshot.AdjacentYearSoftPairs,
            DistributionConstraints = snapshot.DistributionConstraints,
        };
    }

    private static SchedulingConflictDto MapToConflictDto(RuleViolation v) => new()
    {
        ConflictType = v.ConflictType,
        Message = v.Message,
        SectionId = v.SectionId,
        RelatedSectionId = v.RelatedSectionId,
        Day = v.Day ?? 0,
        StartTime = v.StartTime ?? default,
        EndTime = v.EndTime ?? default,
    };

    private static string BuildDedupKey(SchedulingConflictDto c)
    {
        var ids = new[] { c.SectionId, c.RelatedSectionId ?? 0 };
        Array.Sort(ids);
        return $"{c.ConflictType}:{ids[0]}:{ids[1]}:{c.Day}:{c.StartTime}";
    }
}
