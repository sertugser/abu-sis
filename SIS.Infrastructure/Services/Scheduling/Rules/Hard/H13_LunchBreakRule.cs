using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// H13 Öğle Arası Koruması (Hard). Blok (start, end) [LunchBreakStart, LunchBreakEnd)
/// ile çakışıyorsa ihlal. Hard/Soft moda geçişi RuleEngine ResolveCategory yönetir:
/// <c>ConstraintDefinitions["H13"].Category</c> Hard ise bu predicate çağrılır; Soft ise
/// <see cref="Soft.S5_LunchBreakSoftRule"/> çağrılır.
/// </summary>
public sealed class H13_LunchBreakRule : IRulePredicate
{
    public string Code => "H13";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var config = ctx.Config;
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
