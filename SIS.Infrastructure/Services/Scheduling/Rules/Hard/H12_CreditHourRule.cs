using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.12 — H12 Kredi-Saat Uyumu.
/// V2 encoder YOK (V2 atama doğal olarak TheoryHours+LabHours saat üretir). V1 evaluator
/// mantığı port edilir: atanan farklı (Day, Start, End) pencere sayısı (paralel-split aware:
/// aynı pencere bir kez sayılır) section.TheoryHours+LabHours toplamına eşit olmalı.
///
/// Plan 11 Risk R6: manuel atama yanlış slot sayısı üretirse V1 hardly enforce'u runtime'da
/// devam eder. V2 atama bu kuralı her zaman pass eder.
/// </summary>
public sealed class H12_CreditHourRule : IRulePredicate
{
    public string Code => "H12";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        if (c.Blocks.Count == 0) return Array.Empty<RuleViolation>();

        var expectedHours = c.TheoryHours + c.LabHours;
        if (expectedHours <= 0) return Array.Empty<RuleViolation>();

        // Paralel-split aware: aynı (Day, Start, End) penceresi tek saat sayılır.
        var assignedHours = c.Blocks
            .GroupBy(b => (b.Day, b.StartTime, b.EndTime))
            .Sum(g => (int)Math.Ceiling((g.Key.EndTime - g.Key.StartTime).TotalHours));

        if (assignedHours == expectedHours)
            return Array.Empty<RuleViolation>();

        return new[]
        {
            new RuleViolation(
                Code: Code,
                ConflictType: "CreditHourMismatch",
                Message: $"Dersin haftalik slot gereksinimine uygun atama yapilmamis ({c.CourseCode}: beklenen {expectedHours} slot, atanan {assignedHours} slot).",
                SectionId: c.SectionId)
        };
    }
}
