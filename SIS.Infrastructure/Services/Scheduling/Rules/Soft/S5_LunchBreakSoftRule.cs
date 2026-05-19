using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Soft;

/// <summary>
/// H13 Öğle Arası Tercihi (Soft). Her lunch-overlap block için 8 puan penalty.
/// Hard/Soft moda geçişi RuleEngine ResolveCategory yönetir:
/// <c>ConstraintDefinitions["H13"].Category</c> Soft ise bu predicate çağrılır; Hard ise
/// <see cref="Hard.H13_LunchBreakRule"/> çağrılır. Class adı tarihsel sebeple "S5"; Code "H13".
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
