using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.15 — H15 Maks Ardışık Ders Saati.
/// V1 ile V2 birebir aynı semantik: aynı gün içinde, hocanın derslerini start time'a göre sırala,
/// gap ≤ BreakDurationMinutes ise blok birikir; max blok > MaxConsecutiveHours olamaz.
/// </summary>
public sealed class H15_MaxConsecutiveHoursRule : IRulePredicate
{
    public string Code => "H15";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var config = ctx.Config;
        if (!c.InstructorId.HasValue || c.Blocks.Count == 0)
            return Array.Empty<RuleViolation>();

        var allSlots = ctx.Snapshot.Sections
            .Where(s => s.InstructorId == c.InstructorId && s.SectionId != c.SectionId)
            .SelectMany(s => s.Blocks.Select(b => new { b.Day, b.StartTime, b.EndTime }))
            .Concat(c.Blocks.Select(b => new { b.Day, b.StartTime, b.EndTime }))
            .ToList();

        var violations = new List<RuleViolation>();

        foreach (var dayGroup in allSlots.GroupBy(s => s.Day))
        {
            var sorted = dayGroup.OrderBy(s => s.StartTime).ToList();
            if (sorted.Count <= 1) continue;

            var blockMinutes = (sorted[0].EndTime - sorted[0].StartTime).TotalMinutes;
            var maxBlockMinutes = blockMinutes;

            for (int i = 1; i < sorted.Count; i++)
            {
                var gap = (sorted[i].StartTime - sorted[i - 1].EndTime).TotalMinutes;
                if (gap <= config.BreakDurationMinutes)
                    blockMinutes += (sorted[i].EndTime - sorted[i].StartTime).TotalMinutes;
                else
                    blockMinutes = (sorted[i].EndTime - sorted[i].StartTime).TotalMinutes;

                if (blockMinutes > maxBlockMinutes) maxBlockMinutes = blockMinutes;
            }

            var blockHours = maxBlockMinutes / 60.0;
            if (blockHours > config.MaxConsecutiveHours)
            {
                violations.Add(new RuleViolation(
                    Code: Code, ConflictType: "MaxConsecutiveHoursConflict",
                    Message: $"Öğretim üyesi '{c.InstructorName}' {DayName(dayGroup.Key)} günü {config.MaxConsecutiveHours} saatten fazla aralıksız ders veriyor ({blockHours:0.#} saat).",
                    SectionId: c.SectionId, Day: dayGroup.Key));
            }
        }

        return violations;
    }

    private static string DayName(int day) => day switch
    {
        0 => "Pazar", 1 => "Pazartesi", 2 => "Salı", 3 => "Çarşamba",
        4 => "Perşembe", 5 => "Cuma", 6 => "Cumartesi", _ => $"day{day}"
    };
}
