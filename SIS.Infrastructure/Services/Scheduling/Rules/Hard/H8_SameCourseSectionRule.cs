using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// H8 Aynı Ders Şube Çakışması — HYBRID predicate (DIST pattern). Hard/Soft moda geçişi
/// RuleEngine ResolveCategory yönetir: <c>ConstraintDefinitions["H8"].Category</c> Hard ise
/// <see cref="Evaluate"/> hard violation üretir; Soft ise <see cref="CalculatePenalty"/>
/// her overlap pair için <c>H8SoftPenaltyPerPair</c> kadar penalty üretir (V2
/// SameCourseSectionSoftEncoder ile parite).
/// </summary>
public sealed class H8_SameCourseSectionRule : ISoftRulePredicate
{
    // Validator soft penalty per overlapping pair — solver SoftThresholds.H8Weight default
    // (8) ile aynı semantik. RuleEngine sonra def.Weight (kullanıcı ağırlığı) ile çarpar.
    private const double SoftPenaltyPerOverlapPair = 8.0;

    public string Code => "H8";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var violations = new List<RuleViolation>();

        foreach (var existing in ctx.Snapshot.Sections)
        {
            if (existing.SectionId == c.SectionId) continue;
            if (existing.CourseId != c.CourseId) continue;

            foreach (var a in c.Blocks)
            foreach (var b in existing.Blocks)
            {
                if (a.Day != b.Day) continue;
                if (a.StartTime < b.EndTime && a.EndTime > b.StartTime)
                {
                    var ovS = a.StartTime > b.StartTime ? a.StartTime : b.StartTime;
                    var ovE = a.EndTime < b.EndTime ? a.EndTime : b.EndTime;
                    violations.Add(new RuleViolation(
                        Code: Code, ConflictType: "SameCourseSectionConflict",
                        Message: $"Aynı dersin ({c.CourseCode}) farklı şubeleri aynı zaman aralığında çakışıyor.",
                        SectionId: c.SectionId, RelatedSectionId: existing.SectionId,
                        Day: a.Day, StartTime: ovS, EndTime: ovE));
                }
            }
        }

        return violations;
    }

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var c = ctx.Candidate;
        if (c.Blocks.Count == 0) return SoftRuleResult.Zero;

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var existing in ctx.Snapshot.Sections)
        {
            if (existing.SectionId == c.SectionId) continue;
            if (existing.CourseId != c.CourseId) continue;

            foreach (var a in c.Blocks)
            foreach (var b in existing.Blocks)
            {
                if (a.Day != b.Day) continue;
                if (a.StartTime < b.EndTime && a.EndTime > b.StartTime)
                {
                    totalPenalty += SoftPenaltyPerOverlapPair;
                    if (notes.Count < 5)
                        notes.Add($"H8 Uyarı: {c.CourseCode} farklı şubeleri çakışıyor ({a.StartTime:HH\\:mm}-{a.EndTime:HH\\:mm}).");
                }
            }
        }

        return totalPenalty > 0
            ? new SoftRuleResult(totalPenalty, notes.Count, 1, notes)
            : SoftRuleResult.Zero;
    }
}
