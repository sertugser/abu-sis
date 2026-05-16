using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.13 — H13 Öğle Arası Koruması.
/// V1 <c>LunchBreakEvaluator</c> ile V2 <c>LunchBreakEncoder</c> aynı semantik: blok
/// (start, end) [LunchBreakStart, LunchBreakEnd) ile çakışıyorsa ihlal. Plan 10 toggle:
/// <c>LunchBreakIsHard=false</c> ise V1 evaluator soft path'e düşer (predicate de skip eder —
/// effectiveCategory Soft ise RuleEngine zaten çağırmaz; ek olarak config flag de kapanırsa
/// bu predicate kendi başına da skip eder).
/// </summary>
public sealed class H13_LunchBreakRule : IRulePredicate
{
    public string Code => "H13";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var config = ctx.Config;
        if (!config.LunchBreakIsHard)
            return Array.Empty<RuleViolation>();

        var c = ctx.Candidate;
        var violations = new List<RuleViolation>();
        foreach (var block in c.Blocks)
        {
            if (block.StartTime < config.LunchBreakEnd && block.EndTime > config.LunchBreakStart)
            {
                violations.Add(new RuleViolation(
                    Code: Code,
                    ConflictType: "LunchBreakConflict",
                    Message: "Öğle arası saatlerinde ders atanamaz.",
                    SectionId: c.SectionId,
                    Day: block.Day,
                    StartTime: block.StartTime,
                    EndTime: block.EndTime));
            }
        }
        return violations;
    }
}
