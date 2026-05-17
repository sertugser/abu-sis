using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Soft;

/// <summary>
/// Plan 12 Faz 2.6 — S7 Öğrenci Boşluk Minimizasyonu (V1 <c>StudentGapEvaluator</c> portu).
///
/// V1 mantığı: candidate.DepartmentId + SuggestedSemester (year = (sem-1)/2) ile aynı
/// kohorttaki section'ları topla; candidate'in slot'ları ile birleştir; gün başına sırala;
/// ardışık slot arasındaki <c>gap ≥ S7MaxGapSlots</c> için <c>(gap − cap + 1) × 3</c> penalty.
/// </summary>
public sealed class S7_StudentGapRule : ISoftRulePredicate
{
    public string Code => "S7";
    public RuleCategory DefaultCategory => RuleCategory.Soft;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx) => Array.Empty<RuleViolation>();

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var config = ctx.Config;

        if (!config.S7Enabled || c.Blocks.Count == 0
            || !c.DepartmentId.HasValue || !c.SuggestedSemester.HasValue)
            return SoftRuleResult.Zero;

        var candidateYear = (c.SuggestedSemester.Value - 1) / 2;

        var sameDeptYearSections = ctx.Snapshot.Sections
            .Where(s => s.DepartmentId == c.DepartmentId
                && s.SuggestedSemester.HasValue
                && (s.SuggestedSemester.Value - 1) / 2 == candidateYear
                && s.Blocks.Count > 0)
            .ToList();

        var combined = sameDeptYearSections
            .SelectMany(s => s.Blocks.Select(b => new { s.CourseCode, Block = b }))
            .Concat(c.Blocks.Select(b => new { c.CourseCode, Block = b }))
            .GroupBy(x => x.Block.Day);

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var dayGroup in combined)
        {
            var sorted = dayGroup.OrderBy(x => x.Block.StartTime).ToList();
            if (sorted.Count < 2) continue;

            for (int idx = 1; idx < sorted.Count; idx++)
            {
                var gapHours = (sorted[idx].Block.StartTime - sorted[idx - 1].Block.EndTime).TotalHours;
                if (gapHours >= config.S7MaxGapSlots)
                {
                    totalPenalty += (gapHours - config.S7MaxGapSlots + 1) * 3;
                    notes.Add($"S7 Uyarı: {DayName(dayGroup.Key)} günü '{sorted[idx - 1].CourseCode}' ile '{sorted[idx].CourseCode}' arasında {gapHours:0.#} saatlik boşluk var.");
                }
            }
        }

        return totalPenalty > 0
            ? new SoftRuleResult(totalPenalty, notes.Count, 1, notes)
            : SoftRuleResult.Zero;
    }

    private static string DayName(int day) => day switch
    {
        0 => "Pazar", 1 => "Pazartesi", 2 => "Salı", 3 => "Çarşamba",
        4 => "Perşembe", 5 => "Cuma", 6 => "Cumartesi", _ => $"day{day}"
    };
}
