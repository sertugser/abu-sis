using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Soft;

/// <summary>
/// Plan 12 Faz 2.5 — S5 Öğle Arası Tercihi (V1 <c>LunchBreakSoftEvaluator</c> portu).
///
/// V1 davranışı: <c>config.LunchBreakIsHard</c> true ise skip (H13 hard mod aktif);
/// aksi halde her lunch-overlap block için sabit 8 puan.
///
/// Code drift notu (Faz 0.2 → Faz 9): evaluator Code'u "S5" dönüyor ama DB'de S5
/// Definition yok (Plan 10 H13'e birleştirildi). Faz 9'da V2 alignment ile birlikte
/// Code "H13"e çekilir; şimdilik "S5" korunur ki pinning baseline kırılmasın.
/// </summary>
public sealed class S5_LunchBreakSoftRule : ISoftRulePredicate
{
    public string Code => "S5";
    public RuleCategory DefaultCategory => RuleCategory.Soft;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx) => Array.Empty<RuleViolation>();

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var config = ctx.Config;

        if (!config.S5Enabled || c.Blocks.Count == 0) return SoftRuleResult.Zero;
        if (config.LunchBreakIsHard) return SoftRuleResult.Zero;

        var lunchStart = config.LunchBreakStart != default ? config.LunchBreakStart : new TimeOnly(11, 40);
        var lunchEnd = config.LunchBreakEnd != default ? config.LunchBreakEnd : new TimeOnly(12, 20);

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var slot in c.Blocks)
        {
            if (slot.StartTime < lunchEnd && slot.EndTime > lunchStart)
            {
                totalPenalty += 8;
                notes.Add($"S5 Uyarı: {c.CourseCode} öğle arası saatlerinde programlanmış ({slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm}).");
            }
        }

        return totalPenalty > 0
            ? new SoftRuleResult(totalPenalty, notes.Count, 1, notes)
            : SoftRuleResult.Zero;
    }
}
