using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

public sealed class LunchBreakEvaluator : IConstraintEvaluator
{
    public string Code => "H13";
    public string Name => "Öğle Arası Koruması";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var config = context.Config;

        if (!config.LunchBreakIsHard)
            return ConstraintResult.Ok();

        var conflicts = new List<SchedulingConflictDto>();

        foreach (var slot in c.TimeBlocks)
        {
            if (slot.StartTime < config.LunchBreakEnd && slot.EndTime > config.LunchBreakStart)
            {
                conflicts.Add(SchedulingHelpers.BuildConflict(
                    c,
                    "LunchBreakConflict",
                    "Öğle arası saatlerinde ders atanamaz.",
                    slot,
                    null));
            }
        }

        return conflicts.Count > 0
            ? ConstraintResult.WithConflicts(conflicts)
            : ConstraintResult.Ok();
    }
}
