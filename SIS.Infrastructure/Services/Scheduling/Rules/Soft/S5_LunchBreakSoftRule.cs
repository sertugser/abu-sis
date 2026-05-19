using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Soft;

/// <summary>
/// Plan 12 Faz 9 — S5 Öğle Arası Tercihi (V1 <c>LunchBreakSoftEvaluator</c> portu).
///
/// V1 davranışı: <c>config.LunchBreakIsHard</c> true ise skip; aksi halde her
/// lunch-overlap block için sabit 8 puan.
///
/// Code drift fix (2026-05-15): Plan 10'da S5 ConstraintDefinition silindi,
/// H13 Category toggle tek otorite. Code "S5" → "H13":
///   • Admin H13 Category="Hard" → DefaultCategory Soft olsa da ResolveCategory
///     DB'den Hard döner → H13_LunchBreakRule (hard) çalışır, bu predicate skip.
///   • Admin H13 Category="Soft" → ResolveCategory Soft → bu predicate çalışır,
///     hard rule skip.
/// V1 pinning baseline davranışı (S5 her zaman çalışır) Plan 10 mimari spec'inin
/// dışındaydı — Faz 9'da düzeltildi.
/// </summary>
public sealed class S5_LunchBreakSoftRule : ISoftRulePredicate
{
    public string Code => "H13";
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
