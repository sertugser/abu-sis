using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

public sealed class ClassroomConflictEvaluator : IConstraintEvaluator
{
    public string Code => "H1";
    public string Name => "Derslik Çakışması";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;

        if (!c.ClassroomId.HasValue)
            return ConstraintResult.Ok();

        var conflicts = new List<SchedulingConflictDto>();

        foreach (var existing in context.Snapshot.Sections)
        {
            if (c.ClassroomId != existing.ClassroomId)
                continue;

            foreach (var overlap in SchedulingHelpers.GetOverlapSlots(c.TimeBlocks, existing.TimeBlocks))
            {
                conflicts.Add(SchedulingHelpers.BuildConflict(
                    c,
                    "ClassroomConflict",
                    $"Derslik '{c.ClassroomName}' aynı zaman aralığında '{existing.CourseCode}' section'u ile çakışıyor.",
                    overlap,
                    existing.SectionId));
            }
        }

        return conflicts.Count > 0
            ? ConstraintResult.WithConflicts(conflicts)
            : ConstraintResult.Ok();
    }
}
