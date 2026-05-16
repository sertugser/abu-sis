using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.6 — H6 Ders Bileşen Çakışması.
/// V2 TheoryLabComponentEncoder (Plan 9 Task 11) ile parite: same-section + cross-section.
/// Same-section çakışma RuntimeCandidate.Blocks içinde theory ile lab pencerelerinin overlap
/// etmemesi şeklinde inşa edilir. RuntimeBlock'ta IsTheory ayrımı yok — V2'de zaten ayrı
/// IntervalVar'lar oluşturuluyor; runtime'da TheoryHours/LabHours kullanılarak çıkarsama yapılır.
///
/// Cross-section: aynı CourseId'nin lecture-only (TheoryHours&gt;0, LabHours=0) section'unun
/// theory'si ile lab-only (LabHours&gt;0, TheoryHours=0) section'unun lab'ı aynı zamanda olamaz
/// (öğrenci aynı anda iki yerde olamaz).
/// </summary>
public sealed class H6_TheoryLabComponentRule : IRulePredicate
{
    public string Code => "H6";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var violations = new List<RuleViolation>();

        var candidateIsLab = c.LabHours > 0 && c.TheoryHours == 0;
        var candidateIsLecture = c.TheoryHours > 0 && c.LabHours == 0;

        // Cross-section: aynı CourseId, farklı section, lecture vs lab.
        foreach (var existing in ctx.Snapshot.Sections)
        {
            if (existing.SectionId == c.SectionId) continue;
            if (existing.CourseId != c.CourseId) continue;

            var existingIsLab = existing.LabHours > 0 && existing.TheoryHours == 0;
            var existingIsLecture = existing.TheoryHours > 0 && existing.LabHours == 0;

            if (!((candidateIsLab && existingIsLecture) || (candidateIsLecture && existingIsLab)))
                continue;

            foreach (var a in c.Blocks)
            foreach (var b in existing.Blocks)
            {
                if (a.Day != b.Day) continue;
                if (a.StartTime < b.EndTime && a.EndTime > b.StartTime)
                {
                    var ovS = a.StartTime > b.StartTime ? a.StartTime : b.StartTime;
                    var ovE = a.EndTime < b.EndTime ? a.EndTime : b.EndTime;
                    violations.Add(new RuleViolation(
                        Code: Code, ConflictType: "SameCourseComponentConflict",
                        Message: $"Aynı dersin ({c.CourseCode}) lecture ve lab bileşenleri aynı zaman aralığında çakışıyor.",
                        SectionId: c.SectionId, RelatedSectionId: existing.SectionId,
                        Day: a.Day, StartTime: ovS, EndTime: ovE));
                }
            }
        }

        return violations;
    }
}
