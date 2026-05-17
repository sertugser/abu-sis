using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Soft;

/// <summary>
/// Plan 12 Faz 2.7 — S8 Öğretim Üyesi Gün Dağılımı (V1 <c>InstructorDaySpreadEvaluator</c> portu).
///
/// V1 mantığı: hocanın tüm slot'larını (snapshot.Sections same instructor + candidate) topla,
/// usedDays + totalHours hesapla, <c>minPossibleDays = max(1, ceil(totalHours / S2MaxDailyHours))</c>,
/// <c>usedDays &gt; minPossibleDays</c> → <c>(usedDays − minPossibleDays) × 4</c> penalty.
///
/// V1 quirk: candidate snapshot içindeyse iki kez sayılır (V2 dispatch'te bu davranış korunur).
/// </summary>
public sealed class S8_InstructorDaySpreadRule : ISoftRulePredicate
{
    public string Code => "S8";
    public RuleCategory DefaultCategory => RuleCategory.Soft;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx) => Array.Empty<RuleViolation>();

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var config = ctx.Config;

        if (!config.S8Enabled || !c.InstructorId.HasValue || c.Blocks.Count == 0)
            return SoftRuleResult.Zero;

        var existingSlots = ctx.Snapshot.Sections
            .Where(s => s.InstructorId == c.InstructorId)
            .SelectMany(s => s.Blocks)
            .ToList();

        var allSlots = existingSlots.Concat(c.Blocks).ToList();
        var usedDays = allSlots.Select(s => s.Day).Distinct().Count();
        var totalHours = allSlots.Sum(s => (s.EndTime - s.StartTime).TotalHours);
        var maxHoursPerDay = (double)config.S2MaxDailyHours;
        var minPossibleDays = Math.Max(1, (int)Math.Ceiling(totalHours / maxHoursPerDay));

        if (usedDays <= minPossibleDays) return SoftRuleResult.Zero;

        var penalty = (usedDays - minPossibleDays) * 4.0;
        var notes = usedDays > minPossibleDays + 1
            ? new[] { $"S8 Uyarı: {c.InstructorName} hocanın dersleri {usedDays} güne dağılmış (minimum {minPossibleDays} gün yeterli olabilir)." }
            : Array.Empty<string>();

        return new SoftRuleResult(penalty, 1, 1, notes);
    }
}
