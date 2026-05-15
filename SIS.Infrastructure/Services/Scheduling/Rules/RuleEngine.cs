using Microsoft.Extensions.Logging;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules;

/// <summary>
/// Plan 11 — IRulePredicate listesi üzerinde Plan 10 disabled/exempt/category mantığını
/// uygular ve sonuçları <see cref="SchedulingConflictDto"/>'ya map'ler. Bu iskele Faz 2.4'te
/// boş döner — Faz 3 her predicate eklendikçe gerçek orchestration <see cref="EvaluateCandidateAsync"/>
/// ve <see cref="EvaluateAllExistingAsync"/> içine eklenir (Task 3.18).
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
        // Faz 3.18 — orchestration: predicate'ları sıralı çağır, disabled/exempt skip,
        // ResolveCategory ile DB-driven Hard/Soft, violation -> ConflictDto map'leme.
        var conflicts = RunPredicates(candidate, snapshot, config, state);
        return Task.FromResult<IReadOnlyList<SchedulingConflictDto>>(conflicts);
    }

    public Task<IReadOnlyList<SchedulingConflictDto>> EvaluateAllExistingAsync(
        RuntimeSnapshot snapshot,
        SchedulingConstraintConfig config,
        ConstraintState? state,
        CancellationToken ct)
    {
        // Faz 3.18 — her section için filteredSnapshot kurup RunPredicates; bidirectional dedup
        // (V1 ConstraintEvaluationService.EvaluateAllExisting BuildConflictKey portu).
        var seen = new HashSet<string>();
        var all = new List<SchedulingConflictDto>();

        foreach (var section in snapshot.Sections)
        {
            var filtered = FilterSectionOut(snapshot, section.SectionId);
            var perSection = RunPredicates(section, filtered, config, state);
            foreach (var c in perSection)
            {
                var key = BuildDedupKey(c);
                if (seen.Add(key))
                    all.Add(c);
            }
        }

        return Task.FromResult<IReadOnlyList<SchedulingConflictDto>>(all);
    }

    // Predicate'ları sırayla çağır, Plan 10 disable/exempt/category mantığı uygula.
    // Faz 2.4'te boş predicate set olduğu için boş döner; Faz 3.N'lerde gerçek predicate'lar
    // DI'a eklendikçe etkili çalışır.
    private List<SchedulingConflictDto> RunPredicates(
        RuntimeCandidate candidate, RuntimeSnapshot snapshot,
        SchedulingConstraintConfig config, ConstraintState? state)
    {
        var result = new List<SchedulingConflictDto>();
        var ctx = new RuleContext
        {
            Candidate = candidate,
            Snapshot = snapshot,
            Config = config,
            State = state,
        };

        foreach (var predicate in _predicates)
        {
            if (state != null)
            {
                if (state.IsDisabled(predicate.Code)) continue;
                if (state.IsExempt(predicate.Code, candidate.InstructorId, candidate.DepartmentId, candidate.SectionId)) continue;
            }

            var effectiveCategory = ResolveCategory(predicate, state);
            if (effectiveCategory != RuleCategory.Hard)
                continue; // soft path Plan 11 kapsamı dışı

            IReadOnlyList<RuleViolation> violations;
            try
            {
                violations = predicate.Evaluate(ctx);
            }
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
