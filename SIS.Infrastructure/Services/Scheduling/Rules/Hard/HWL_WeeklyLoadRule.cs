using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.WL — H-WL Haftalık Hoca Yükü.
/// V2 mantığı seçildi (Plan 11 R10): <c>overrideMax > 0 ? overrideMax : DefaultInstructorMaxWeeklyHours</c>.
/// V1'in disabled-when-zero davranışı kaldırılır — instructor.MaxWeeklyHours=0 olan kişiler için
/// default 30 saat sınırı uygulanır. Plan 11 risk R10 onaylandı.
///
/// DefaultInstructorMaxWeeklyHours bu predicate için context.Config'te alan yok; SystemConfig
/// üzerinden direkt RuleEngine factory'sine inject edilmesi gerek (Faz 4'te eklenecek).
/// Bu predicate şu an sadece override > 0 ise enforce eder; default fallback Faz 4'te aktive.
/// </summary>
public sealed class HWL_WeeklyLoadRule : IRulePredicate
{
    public string Code => "H-WL";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        if (!c.InstructorId.HasValue) return Array.Empty<RuleViolation>();

        // TODO Faz 4: DefaultInstructorMaxWeeklyHours SystemConfig'ten okunup config'e eklenecek.
        // Şu an V1 paritesi: override > 0 ise enforce, değilse skip (V1 disabled-when-zero).
        if (c.InstructorMaxWeeklyHours <= 0) return Array.Empty<RuleViolation>();
        var maxWeekly = c.InstructorMaxWeeklyHours;

        var totalHours = ctx.Snapshot.Sections
            .Where(s => s.InstructorId == c.InstructorId && s.SectionId != c.SectionId)
            .SelectMany(s => s.Blocks
                .GroupBy(b => (b.Day, b.StartTime, b.EndTime)) // paralel-split aware
                .Select(g => (g.Key.EndTime - g.Key.StartTime).TotalHours))
            .Sum()
            + c.Blocks
                .GroupBy(b => (b.Day, b.StartTime, b.EndTime))
                .Sum(g => (g.Key.EndTime - g.Key.StartTime).TotalHours);

        if (totalHours > maxWeekly)
        {
            return new[]
            {
                new RuleViolation(
                    Code: Code, ConflictType: "WeeklyLoadConflict",
                    Message: $"Instructor haftalik ders yuku limiti asiliyor ({totalHours:0.##}/{maxWeekly} saat).",
                    SectionId: c.SectionId)
            };
        }

        return Array.Empty<RuleViolation>();
    }
}
