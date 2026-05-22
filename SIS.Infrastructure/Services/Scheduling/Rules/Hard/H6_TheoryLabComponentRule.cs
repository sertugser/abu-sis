using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.6 — H6 Ders Bileşen Çakışması.
/// V2 TheoryLabComponentEncoder (Plan 9 Task 11) ile parite: same-section + cross-section.
///
/// Same-section: aynı şubenin Theory bloğu ile Lab bloğu aynı zaman aralığında çakışamaz —
/// öğrenci aynı saatte iki yerde olamaz. RuntimeBlock.BlockType bilgisi kullanılır; paralel-split
/// (aynı pencere + aynı BlockType + farklı ClassroomId) meşru senaryo olarak istisna tutulur.
///
/// Cross-section: aynı CourseId'nin lecture-only (TheoryHours&gt;0, LabHours=0) section'unun
/// theory'si ile lab-only (LabHours&gt;0, TheoryHours=0) section'unun lab'ı aynı zamanda olamaz.
/// </summary>
public sealed class H6_TheoryLabComponentRule : IRulePredicate
{
    public string Code => "H6";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var violations = new List<RuleViolation>();

        // ── Same-section: kendi blokları arasında Theory + Lab overlap'i ──
        var blocks = c.Blocks;
        if (blocks.Count >= 2)
        {
            var reported = new HashSet<(int, TimeOnly, TimeOnly)>();
            for (var i = 0; i < blocks.Count; i++)
            for (var j = i + 1; j < blocks.Count; j++)
            {
                var a = blocks[i];
                var b = blocks[j];
                if (a.Day != b.Day) continue;
                if (!(a.StartTime < b.EndTime && a.EndTime > b.StartTime)) continue;

                // Paralel-split istisnası: tam aynı pencere + aynı BlockType (veya BlockType yoksa
                // eski davranış için skip) → farklı classroom'lara yayılma meşru.
                var samePencere = a.StartTime == b.StartTime && a.EndTime == b.EndTime;
                if (samePencere)
                {
                    if (a.BlockType == null || b.BlockType == null) continue;
                    if (a.BlockType == b.BlockType) continue;
                }

                var ovStart = a.StartTime > b.StartTime ? a.StartTime : b.StartTime;
                var ovEnd = a.EndTime < b.EndTime ? a.EndTime : b.EndTime;
                var key = (a.Day, ovStart, ovEnd);
                if (!reported.Add(key)) continue;

                var typeNote = (a.BlockType.HasValue && b.BlockType.HasValue && a.BlockType != b.BlockType)
                    ? $" ({BlockTypeLabel(a.BlockType.Value)} ile {BlockTypeLabel(b.BlockType.Value)})"
                    : string.Empty;

                violations.Add(new RuleViolation(
                    Code: Code,
                    ConflictType: "SameSectionComponentConflict",
                    Message:
                        $"Aynı şubenin ({c.CourseCode}) iki bloğu aynı zaman aralığında çakışıyor: " +
                        $"{DayLabel(a.Day)} {ovStart:HH\\:mm}-{ovEnd:HH\\:mm}{typeNote}. " +
                        $"Öğrenci aynı saatte iki yerde olamaz.",
                    SectionId: c.SectionId,
                    Day: a.Day,
                    StartTime: ovStart,
                    EndTime: ovEnd));
            }
        }

        // ── Cross-section: aynı CourseId, lecture-only vs lab-only farklı section'lar ──
        var candidateIsLab = c.LabHours > 0 && c.TheoryHours == 0;
        var candidateIsLecture = c.TheoryHours > 0 && c.LabHours == 0;

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

    private static string BlockTypeLabel(ScheduleBlockType t) => t switch
    {
        ScheduleBlockType.Theory => "Teori",
        ScheduleBlockType.Lab => "Lab",
        _ => t.ToString(),
    };

    private static string DayLabel(int day) => day switch
    {
        1 => "Pazartesi",
        2 => "Salı",
        3 => "Çarşamba",
        4 => "Perşembe",
        5 => "Cuma",
        6 => "Cumartesi",
        0 => "Pazar",
        _ => $"Gün {day}",
    };
}
