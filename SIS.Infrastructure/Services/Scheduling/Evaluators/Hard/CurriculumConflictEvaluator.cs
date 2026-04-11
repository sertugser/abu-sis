using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

/// <summary>
/// H5: Aynı bölüm/yıl öğrencilerinin alabileceği dersler aynı slot'ta olamaz.
/// BUG FIX B18: Elective courses are now included in the check (not just compulsory).
/// </summary>
internal sealed class CurriculumConflictEvaluator : IConstraintEvaluator
{
    public string Code => "H5";
    public string Name => "Müfredat Çakışması";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var conflicts = new List<SchedulingConflictDto>();

        foreach (var existing in context.Snapshot.Sections)
        {
            if (c.CourseId == existing.CourseId)
                continue;

            if (!SchedulingHelpers.TryGetSharedCurriculumDepartment(c, existing, out _))
                continue;

            foreach (var overlap in SchedulingHelpers.GetOverlapSlots(c.TimeBlocks, existing.TimeBlocks))
            {
                conflicts.Add(SchedulingHelpers.BuildConflict(
                    c,
                    "CurriculumConflict",
                    $"Aynı bölüm/yıl öğrencilerinin alabileceği '{c.CourseCode}' ile '{existing.CourseCode}' dersleri aynı zaman aralığında çakışıyor.",
                    overlap,
                    existing.SectionId));
            }
        }

        return conflicts.Count > 0
            ? ConstraintResult.WithConflicts(conflicts)
            : ConstraintResult.Ok();
    }
}
