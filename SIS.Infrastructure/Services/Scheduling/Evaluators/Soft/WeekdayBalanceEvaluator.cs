using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

internal sealed class WeekdayBalanceEvaluator : IConstraintEvaluator
{
    public string Code => "S12";
    public string Name => "Hafta İçi Denge";
    public ConstraintCategory Category => ConstraintCategory.Soft;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var config = context.Config;

        if (!config.S12Enabled || c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        var existingSlots = context.Snapshot.Sections.SelectMany(s => s.TimeBlocks).ToList();
        var allSlots = existingSlots.Concat(c.TimeBlocks);

        var slotsPerDay = allSlots
            .GroupBy(s => s.Day)
            .Select(g => g.Count())
            .ToList();

        if (slotsPerDay.Count <= 1)
            return ConstraintResult.Ok();

        var avg = slotsPerDay.Average();
        var maxCount = slotsPerDay.Max();
        var minCount = slotsPerDay.Min();
        var diff = maxCount - minCount;

        if (avg > 0 && diff > avg * config.S12MaxImbalancePercent / 100.0)
        {
            var penalty = diff * 2.0;
            return ConstraintResult.WithPenalty(penalty,
                $"S12 Uyarı: Hafta içi slot dağılımı dengesiz (min: {minCount}, max: {maxCount}, fark: {diff}).");
        }

        return ConstraintResult.Ok();
    }
}
