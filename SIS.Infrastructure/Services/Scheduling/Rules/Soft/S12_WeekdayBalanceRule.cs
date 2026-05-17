using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Soft;

/// <summary>
/// Plan 12 Faz 2.8 — S12 Hafta İçi Denge (V1 <c>WeekdayBalanceEvaluator</c> portu).
///
/// V1 mantığı: snapshot tüm sections + candidate'in slot'ları → gün başına slot sayısı.
/// Threshold guard: <c>diff &gt; avg × S12MaxImbalancePercent/100</c> → <c>diff × 2.0</c> penalty.
/// </summary>
public sealed class S12_WeekdayBalanceRule : ISoftRulePredicate
{
    public string Code => "S12";
    public RuleCategory DefaultCategory => RuleCategory.Soft;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx) => Array.Empty<RuleViolation>();

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var config = ctx.Config;

        if (!config.S12Enabled || c.Blocks.Count == 0) return SoftRuleResult.Zero;

        var existingSlots = ctx.Snapshot.Sections.SelectMany(s => s.Blocks).ToList();
        var allSlots = existingSlots.Concat(c.Blocks);

        var slotsPerDay = allSlots
            .GroupBy(s => s.Day)
            .Select(g => g.Count())
            .ToList();

        if (slotsPerDay.Count <= 1) return SoftRuleResult.Zero;

        var avg = slotsPerDay.Average();
        var maxCount = slotsPerDay.Max();
        var minCount = slotsPerDay.Min();
        var diff = maxCount - minCount;

        if (avg > 0 && diff > avg * config.S12MaxImbalancePercent / 100.0)
        {
            var penalty = diff * 2.0;
            return new SoftRuleResult(penalty, 1, 1, new[]
            {
                $"S12 Uyarı: Hafta içi slot dağılımı dengesiz (min: {minCount}, max: {maxCount}, fark: {diff})."
            });
        }

        return SoftRuleResult.Zero;
    }
}
