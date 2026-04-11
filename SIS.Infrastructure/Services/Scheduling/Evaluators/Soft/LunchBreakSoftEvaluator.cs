using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

public sealed class LunchBreakSoftEvaluator : IConstraintEvaluator
{
    public string Code => "S5";
    public string Name => "Öğle Arası Tercihi";
    public ConstraintCategory Category => ConstraintCategory.Soft;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var config = context.Config;

        if (!config.S5Enabled || c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        // Only applies when H13 is NOT hard
        if (config.LunchBreakIsHard)
            return ConstraintResult.Ok();

        var lunchStart = config.LunchBreakStart != default ? config.LunchBreakStart : new TimeOnly(11, 40);
        var lunchEnd = config.LunchBreakEnd != default ? config.LunchBreakEnd : new TimeOnly(12, 20);

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var slot in c.TimeBlocks)
        {
            if (slot.StartTime < lunchEnd && slot.EndTime > lunchStart)
            {
                totalPenalty += 8;
                notes.Add($"S5 Uyarı: {c.CourseCode} öğle arası saatlerinde programlanmış ({slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm}).");
            }
        }

        if (totalPenalty > 0)
            return new ConstraintResult { SoftPenalty = totalPenalty, Notes = notes };

        return ConstraintResult.Ok();
    }
}
