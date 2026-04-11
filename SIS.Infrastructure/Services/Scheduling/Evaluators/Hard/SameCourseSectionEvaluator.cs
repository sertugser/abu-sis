using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

public sealed class SameCourseSectionEvaluator : IConstraintEvaluator
{
    public string Code => "H8";
    public string Name => "Aynı Ders Şube Çakışması";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var conflicts = new List<SchedulingConflictDto>();

        foreach (var existing in context.Snapshot.Sections)
        {
            if (c.CourseId != existing.CourseId || c.SectionId == existing.SectionId)
                continue;

            foreach (var overlap in SchedulingHelpers.GetOverlapSlots(c.TimeBlocks, existing.TimeBlocks))
            {
                conflicts.Add(SchedulingHelpers.BuildConflict(
                    c,
                    "SameCourseSectionConflict",
                    $"Aynı dersin ({c.CourseCode}) farklı şubeleri aynı zaman aralığında çakışıyor.",
                    overlap,
                    existing.SectionId));
            }
        }

        return conflicts.Count > 0
            ? ConstraintResult.WithConflicts(conflicts)
            : ConstraintResult.Ok();
    }
}
