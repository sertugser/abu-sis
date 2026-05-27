using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.16 — H16 Teori-Lab Gün Ayrımı.
/// V2 sıkı mantığı seçildi (Plan 11 R7 onayı): toplam (TheoryHours+LabHours) eşiği aşıyorsa
/// theory ve lab AYRI günlerde olmalı. V1'in "aynı gün ama lab teoriden sonra olabilir"
/// daha permissive davranışı kapatılır.
///
/// Theory penceresi tespit: RuntimeBlock'lar üzerinde TheoryHours saat süren (Day, Start, End)
/// pencere theory'dir; kalan bloklar lab. Multi-block lab desteklenir.
/// </summary>
public sealed class H16_TheoryLabDaySplitRule : ISoftRulePredicate
{
    public string Code => "H16";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    // Soft modda (admin "Tercihli") penalty: ihlal başına solver SoftThresholds.H16Weight default'u (5).
    // RuleEngine sonra def.Weight (kullanıcı ağırlığı) ile çarpar — H8/DIST hybrid pattern.
    private const double SoftPenaltyPerViolation = 5;

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var violations = Evaluate(ctx);
        if (violations.Count == 0) return SoftRuleResult.Zero;
        var notes = violations.Take(5).Select(v => v.Message).ToList();
        var sections = violations.Select(v => v.SectionId).Distinct().Count();
        return new SoftRuleResult(violations.Count * SoftPenaltyPerViolation, violations.Count, sections, notes);
    }

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        if (c.TheoryHours <= 0 || c.LabHours <= 0) return Array.Empty<RuleViolation>();

        var total = c.TheoryHours + c.LabHours;
        if (total <= ctx.Config.TheoryLabSplitThreshold) return Array.Empty<RuleViolation>();
        if (c.Blocks.Count == 0) return Array.Empty<RuleViolation>();

        // Theory penceresi: TheoryHours saat süren ilk (Day, Start, End) penceresi.
        var theoryWindow = c.Blocks
            .GroupBy(b => (b.Day, b.StartTime, b.EndTime))
            .Where(g => (int)Math.Round((g.Key.EndTime - g.Key.StartTime).TotalHours) == c.TheoryHours)
            .Select(g => g.Key)
            .FirstOrDefault();

        if (theoryWindow.Equals(default((int, TimeOnly, TimeOnly))))
            return Array.Empty<RuleViolation>(); // tespit edilemedi

        // Lab blokları: theory penceresi dışında kalanlar.
        var labBlocks = c.Blocks
            .GroupBy(b => (b.Day, b.StartTime, b.EndTime))
            .Where(g => !g.Key.Equals(theoryWindow))
            .Select(g => g.Key)
            .ToList();

        var violations = new List<RuleViolation>();
        foreach (var lab in labBlocks)
        {
            if (lab.Day == theoryWindow.Day)
            {
                violations.Add(new RuleViolation(
                    Code: Code, ConflictType: "TheoryLabDaySplitConflict",
                    Message: $"Karma ders '{c.CourseCode}': teori ve lab aynı günde olamaz (eşik: {ctx.Config.TheoryLabSplitThreshold} saat, toplam: {total}).",
                    SectionId: c.SectionId, Day: lab.Day, StartTime: lab.StartTime, EndTime: lab.EndTime));
            }
        }

        return violations;
    }
}
