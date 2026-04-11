using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

internal sealed class NoDayOffEvaluator : IConstraintEvaluator
{
    public string Code => "H-DO";
    public string Name => "Boş Gün Kontrolü";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;

        if (!c.InstructorId.HasValue || c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        var instructorSections = context.Snapshot.Sections
            .Where(s => s.InstructorId == c.InstructorId && s.TimeBlocks.Count > 0)
            .ToList();

        var usedDays = instructorSections
            .SelectMany(s => s.TimeBlocks)
            .Select(slot => slot.Day)
            .Concat(c.TimeBlocks.Select(slot => slot.Day))
            .Distinct()
            .Count();

        if (usedDays >= 5)
        {
            return ConstraintResult.WithConflict(SchedulingHelpers.BuildConflict(
                c,
                "NoDayOffConflict",
                $"Öğretim üyesi '{c.InstructorName}' haftanın 5 gününde de ders veriyor, en az 1 gün boş olmalıdır.",
                null,
                null));
        }

        return ConstraintResult.Ok();
    }
}
