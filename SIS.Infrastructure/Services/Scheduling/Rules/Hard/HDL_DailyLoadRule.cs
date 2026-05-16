using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.DL — H-DL Günlük Hoca Yükü.
/// V2 DailyLoadEncoder ile parite: <c>config.MaxDailyHours</c> SystemConfig (Plan 11 Faz 1.2'de
/// eklendi, default 9). Plan 10 Category toggle Soft yaparsa RuleEngine bu predicate'i çağırmaz.
/// </summary>
public sealed class HDL_DailyLoadRule : IRulePredicate
{
    public string Code => "H-DL";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        if (!c.InstructorId.HasValue || c.Blocks.Count == 0)
            return Array.Empty<RuleViolation>();

        var maxDaily = ctx.Config.MaxDailyHours;

        var instructorSections = ctx.Snapshot.Sections
            .Where(s => s.InstructorId == c.InstructorId && s.SectionId != c.SectionId && s.Blocks.Count > 0)
            .ToList();

        var dailyGroups = instructorSections
            .SelectMany(s => s.Blocks.Select(b => new { b.Day, b.StartTime, b.EndTime }))
            .Concat(c.Blocks.Select(b => new { b.Day, b.StartTime, b.EndTime }))
            .GroupBy(b => b.Day);

        var violations = new List<RuleViolation>();
        foreach (var dayGroup in dailyGroups)
        {
            // Paralel-split aware: aynı pencere tek sayım.
            var dailyHours = dayGroup
                .GroupBy(b => (b.StartTime, b.EndTime))
                .Sum(g => (g.Key.EndTime - g.Key.StartTime).TotalHours);

            if (dailyHours <= maxDaily) continue;

            violations.Add(new RuleViolation(
                Code: Code, ConflictType: "DailyLoadConflict",
                Message: $"Öğretim üyesi '{c.InstructorName}' {DayName(dayGroup.Key)} günü {dailyHours:0.#} saat ders veriyor (maks {maxDaily} saat).",
                SectionId: c.SectionId, Day: dayGroup.Key));
        }
        return violations;
    }

    private static string DayName(int day) => day switch
    {
        0 => "Pazar", 1 => "Pazartesi", 2 => "Salı", 3 => "Çarşamba",
        4 => "Perşembe", 5 => "Cuma", 6 => "Cumartesi", _ => $"day{day}"
    };
}
