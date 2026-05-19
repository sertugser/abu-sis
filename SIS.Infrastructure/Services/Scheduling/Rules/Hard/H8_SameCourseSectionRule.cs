using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.8 — H8 Aynı Ders Şube Çakışması.
/// Aynı CourseId'nin farklı section'ları aynı zamanda olamaz (öğrenci herhangi birini seçebileceği için).
/// <c>SchedulingConstraintConfig.AllowSameCourseSectionOverlap</c> true ise kural tamamen kapalı (V2 paritesi).
/// </summary>
public sealed class H8_SameCourseSectionRule : IRulePredicate
{
    public string Code => "H8";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        if (ctx.Config.AllowSameCourseSectionOverlap)
            return Array.Empty<RuleViolation>();

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
}
