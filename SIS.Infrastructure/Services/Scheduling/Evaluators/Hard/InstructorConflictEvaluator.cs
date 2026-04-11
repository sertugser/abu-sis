using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

internal sealed class InstructorConflictEvaluator : IConstraintEvaluator
{
    public string Code => "H2";
    public string Name => "Öğretim Üyesi Çakışması";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;

        if (!c.InstructorId.HasValue)
            return ConstraintResult.Ok();

        var conflicts = new List<SchedulingConflictDto>();

        foreach (var existing in context.Snapshot.Sections)
        {
            if (c.InstructorId != existing.InstructorId)
                continue;

            foreach (var overlap in SchedulingHelpers.GetOverlapSlots(c.TimeBlocks, existing.TimeBlocks))
            {
                conflicts.Add(SchedulingHelpers.BuildConflict(
                    c,
                    "InstructorConflict",
                    $"Öğretim üyesi '{c.InstructorName}' aynı zaman aralığında '{existing.CourseCode}' section'u ile çakışıyor.",
                    overlap,
                    existing.SectionId));
            }
        }

        return conflicts.Count > 0
            ? ConstraintResult.WithConflicts(conflicts)
            : ConstraintResult.Ok();
    }
}
