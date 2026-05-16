using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.DO — H-DO Boş Gün Garantisi.
/// V2 generalize mantığı seçildi: <c>usedDays ≤ workdays.Count - 1</c> (Plan 11 R11 kararı).
/// V1 hardcoded "≥ 5" sadece 5-günlük workday için aynı. Workday set'i RuntimeSnapshot içindeki
/// section block'larından çıkarılır (TimeSlot listesi snapshot'a yansımıyorsa fallback "Mon-Fri").
/// </summary>
public sealed class HDO_NoDayOffRule : IRulePredicate
{
    public string Code => "H-DO";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        if (!c.InstructorId.HasValue || c.Blocks.Count == 0)
            return Array.Empty<RuleViolation>();

        // Workdays: snapshot'ta bilinen tüm section block'ların distinct günleri (default Mon-Fri 1..5)
        // Faz 2.5 RuntimeSnapshotFactory TimeSlotInput'tan workdays alanı ekleyebilir; şimdilik
        // snapshot'taki gerçek atamalardan türetiyoruz.
        var workdays = ctx.Snapshot.Sections
            .SelectMany(s => s.Blocks.Select(b => b.Day))
            .Concat(c.Blocks.Select(b => b.Day))
            .Distinct()
            .ToList();

        if (workdays.Count <= 1) return Array.Empty<RuleViolation>();

        var maxAllowed = workdays.Count - 1;

        var usedDays = ctx.Snapshot.Sections
            .Where(s => s.InstructorId == c.InstructorId && s.SectionId != c.SectionId)
            .SelectMany(s => s.Blocks.Select(b => b.Day))
            .Concat(c.Blocks.Select(b => b.Day))
            .Distinct()
            .Count();

        if (usedDays > maxAllowed)
        {
            return new[]
            {
                new RuleViolation(
                    Code: Code, ConflictType: "NoDayOffConflict",
                    Message: $"Öğretim üyesi '{c.InstructorName}' haftanın {usedDays} gününde de ders veriyor, en az 1 gün boş olmalıdır (max {maxAllowed} gün).",
                    SectionId: c.SectionId)
            };
        }

        return Array.Empty<RuleViolation>();
    }
}
