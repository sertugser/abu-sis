using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.4 — H4 Derslik Tipi.
/// V2 ClassroomTypeEncoder ile parite. 4 alt-kural:
///   1. Lab section (LabHours&gt;0, TheoryHours=0) → Lab/Computer kategorisinde olmalı.
///   2. Theory section (TheoryHours&gt;0, LabHours=0) → Lab kategorisine yerleştirilmemeli
///      (scarce kaynak koruma — V2 lectureOnly default).
///   3. Capacity ≥ AmphiThreshold → Amphitheater zorunlu, AmphiThresholdEnabled flag-gated
///      (Plan 11 Faz 1.4 drift fix; default false).
///   4. RequiredTheoryClassroomType / RequiredLabClassroomType override — ClassroomTypeRequirementEnabled
///      flag açıkken aktif (Plan 9 Issue 6); ş u sürümde flag SystemConfig'ten okunmuyor →
///      bu predicate sadece kural 1/2/3 enforce eder; admin UI Plan 10 ekleyince predicate genişletilir.
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
        var primaryBlock = c.Blocks[0];

        // (1) Lab section → Lab/Computer.
        if (c.LabHours > 0 && c.TheoryHours == 0
            && primaryBlock.ClassroomType != ClassroomType.Lab
            && primaryBlock.ClassroomType != ClassroomType.Computer)
        {
            violations.Add(new RuleViolation(
                Code: Code, ConflictType: "ClassroomTypeConflict",
                Message: $"Lab section'u ({c.CourseCode}) Lab veya Computer tipinde bir dersliğe atanmalıdır, mevcut tip: {primaryBlock.ClassroomType}.",
                SectionId: c.SectionId));
        }

        // (2) Theory section LabCategory'e veto.
        if (c.TheoryHours > 0 && c.LabHours == 0
            && primaryBlock.ClassroomType.IsLabCategory())
        {
            violations.Add(new RuleViolation(
                Code: Code, ConflictType: "ClassroomTypeConflict",
                Message: $"Teori dersi ({c.CourseCode}) lab/uzman tipi dersliğe atanamaz, mevcut tip: {primaryBlock.ClassroomType}.",
                SectionId: c.SectionId));
        }

        // (3) Amphi rule — flag-gated.
        if (config.AmphiThresholdEnabled
            && c.Capacity >= config.AmphiThreshold
            && primaryBlock.ClassroomType != ClassroomType.Amphitheater)
        {
            violations.Add(new RuleViolation(
                Code: Code, ConflictType: "ClassroomTypeConflict",
                Message: $"Kontenjanı {c.Capacity} olan ders için Amfi derslik gereklidir.",
                SectionId: c.SectionId));
        }

        return violations;
    }
}
