using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Soft;

/// <summary>
/// Plan 12 Faz 9 — S2 Günlük Yoğunluk Dengesi (V1 <c>DailyLoadSoftEvaluator</c> portu).
///
/// V1 davranışı: per-instructor per-day clock-hour toplamı &gt; S2MaxDailyHours →
/// <c>(total − cap) × 10</c>. existingSlots + c.Blocks (V1 quirk: candidate çift sayılır).
///
/// Code drift fix (2026-05-15): Plan 10'da S2 ConstraintDefinition silindi, H-DL
/// Category toggle tek otorite. Code "S2" → "H-DL":
///   • Admin H-DL Category="Hard" → HDL_DailyLoadRule (hard) çalışır, bu predicate skip.
///   • Admin H-DL Category="Soft" → bu predicate (soft penalty) çalışır, hard rule skip.
/// V1 pinning'i Plan 10 mimari spec'inin dışındaydı — Faz 9'da düzeltildi.
/// </summary>
public sealed class S2_DailyLoadSoftRule : ISoftRulePredicate
{
    public string Code => "H-DL";
    public RuleCategory DefaultCategory => RuleCategory.Soft;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx) => Array.Empty<RuleViolation>();

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var config = ctx.Config;

        if (!config.S2Enabled || !c.InstructorId.HasValue || c.Blocks.Count == 0)
            return SoftRuleResult.Zero;

        var existingSlots = ctx.Snapshot.Sections
            .Where(s => s.InstructorId == c.InstructorId)
            .SelectMany(s => s.Blocks)
            .ToList();

        var allSlots = existingSlots.Concat(c.Blocks).ToList();

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var dayGroup in allSlots.GroupBy(b => b.Day))
        {
            var dailyHours = dayGroup.Sum(b => (b.EndTime - b.StartTime).TotalHours);
            if (dailyHours > config.S2MaxDailyHours)
            {
                totalPenalty += (dailyHours - config.S2MaxDailyHours) * 10;
                notes.Add($"S2 Uyarı: {c.InstructorName} hocanın {DayName(dayGroup.Key)} günündeki ders yükü fazla ({dailyHours:0.#} saat, max {config.S2MaxDailyHours})");
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
