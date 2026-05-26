using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// H4 Derslik Tipi. V2 ClassroomTypeEncoder ile parite. 4 alt-kural:
///   1. Lab section (LabHours&gt;0, TheoryHours=0) → Lab/Computer kategorisinde olmalı.
///   2. Theory section (TheoryHours&gt;0, LabHours=0) → Lab kategorisine yerleştirilmemeli
///      (scarce kaynak koruma — V2 lectureOnly default).
///   3. Capacity ≥ AmphiThreshold → Amphitheater zorunlu, AmphiThresholdEnabled flag-gated
///      (default false).
///   4. ClassroomTypeRequirementEnabled flag açıkken Course.RequiredTheoryClassroomType /
///      Course.RequiredLabClassroomType admin override'ı uygulanır (2026-05-19 — predicate'e
///      eklendi, daha önce yalnızca solver biliyordu).
/// </summary>
public sealed class H4_ClassroomTypeRule : IRulePredicate
{
    public string Code => "H4";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var config = ctx.Config;
        if (c.Blocks.Count == 0) return Array.Empty<RuleViolation>();

        var violations = new List<RuleViolation>();
        var isLabSection = c.LabHours > 0 && c.TheoryHours == 0;
        var isTheorySection = c.TheoryHours > 0 && c.LabHours == 0;
        var requiredType = isLabSection ? c.RequiredLabClassroomType : c.RequiredTheoryClassroomType;
        var requirementActive = config.ClassroomTypeRequirementEnabled && requiredType.HasValue;

        // TÜM bloklar kontrol edilir (yalnız Blocks[0] değil): paralel-lab (2+ oda) veya teori+lab
        // farklı odalardaysa, blok 0 dışındaki bir bloğun yanlış-tip dersliği eskiden kaçıyordu.
        // Aynı tip mesajları downstream dedup edilir; farklı odalar farklı mesaj üretir.
        var flaggedTypes = new HashSet<ClassroomType>();
        foreach (var block in c.Blocks)
        {
            if (!flaggedTypes.Add(block.ClassroomType)) continue; // aynı tip için tek mesaj yeter

            // (1) Lab section → Lab/Computer. (Admin override aktifse fallback baypas.)
            if (isLabSection && !requirementActive
                && block.ClassroomType != ClassroomType.Lab
                && block.ClassroomType != ClassroomType.Computer)
            {
                violations.Add(new RuleViolation(
                    Code: Code, ConflictType: "ClassroomTypeConflict",
                    Message: $"Lab section'u ({c.CourseCode}) Lab veya Computer tipinde bir dersliğe atanmalıdır, mevcut tip: {block.ClassroomType}.",
                    SectionId: c.SectionId));
            }

            // (2) Theory section LabCategory'e veto.
            if (isTheorySection && !requirementActive && block.ClassroomType.IsLabCategory())
            {
                violations.Add(new RuleViolation(
                    Code: Code, ConflictType: "ClassroomTypeConflict",
                    Message: $"Teori dersi ({c.CourseCode}) lab/uzman tipi dersliğe atanamaz, mevcut tip: {block.ClassroomType}.",
                    SectionId: c.SectionId));
            }

            // (3) Amphi rule — flag-gated.
            if (config.AmphiThresholdEnabled
                && c.Capacity >= config.AmphiThreshold
                && block.ClassroomType != ClassroomType.Amphitheater)
            {
                violations.Add(new RuleViolation(
                    Code: Code, ConflictType: "ClassroomTypeConflict",
                    Message: $"Kontenjanı {c.Capacity} olan ders için Amfi derslik gereklidir.",
                    SectionId: c.SectionId));
            }

            // (4) Course catalog override — ders kataloğunda tip belirtilmiş ve flag açıksa zorunlu.
            if (requirementActive && (int)block.ClassroomType != requiredType!.Value)
            {
                violations.Add(new RuleViolation(
                    Code: Code, ConflictType: "ClassroomTypeConflict",
                    Message: $"Ders kataloğunda ({c.CourseCode}) {(isLabSection ? "lab" : "teori")} bileşeni için belirtilen derslik tipi (kod: {requiredType}) ile mevcut atanan tip ({block.ClassroomType}) uyuşmuyor.",
                    SectionId: c.SectionId));
            }
        }

        return violations;
    }
}
