using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Soft;

/// <summary>
/// Plan 12 Faz 2.1 — S1 Doluluk Oranı Optimizasyonu (V1 <c>OccupancyEvaluator</c> portu).
/// Penalty formülü birebir korunur (Plan 12 Faz 0.1 pinning baseline):
///   • <c>occupancyRate &lt; S1MinOccupancy</c> → <c>(S1MinOccupancy − occupancyRate) × 0.5</c>
///   • <c>occupancyRate &gt; S1MaxOccupancy</c> → <c>(occupancyRate − S1MaxOccupancy) × 0.5</c>
/// </summary>
public sealed class S1_OccupancyRule : ISoftRulePredicate
{
    public string Code => "S1";
    public RuleCategory DefaultCategory => RuleCategory.Soft;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx) => Array.Empty<RuleViolation>();

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var config = ctx.Config;

        if (!config.S1Enabled) return SoftRuleResult.Zero;
        if (c.Blocks.Count == 0) return SoftRuleResult.Zero;

        // V1 SectionSnapshot.ClassroomId + ClassroomCapacity per-section'du; RuntimeBlock
        // primary classroom değerlerini her bloğa yansıtıyor (RuntimeSnapshotAdapter).
        var primary = c.Blocks[0];
        if (!primary.ClassroomId.HasValue || primary.ClassroomCapacity <= 0)
            return SoftRuleResult.Zero;

        var occupancyRate = c.Capacity * 100.0 / primary.ClassroomCapacity;

        if (occupancyRate < config.S1MinOccupancy)
        {
            var penalty = (config.S1MinOccupancy - occupancyRate) * 0.5;
            return new SoftRuleResult(penalty, 1, 1, new[]
            {
                $"S1 Uyarı: {c.CourseCode} ({primary.ClassroomName}) doluluk oranı düşük (%{occupancyRate:0.#}, hedef >=%{config.S1MinOccupancy})"
            });
        }

        if (occupancyRate > config.S1MaxOccupancy)
        {
            var penalty = (occupancyRate - config.S1MaxOccupancy) * 0.5;
            return new SoftRuleResult(penalty, 1, 1, new[]
            {
                $"S1 Uyarı: {c.CourseCode} ({primary.ClassroomName}) doluluk oranı yüksek (%{occupancyRate:0.#}, hedef <=%{config.S1MaxOccupancy})"
            });
        }

        return SoftRuleResult.Zero;
    }
}
