using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

public sealed class ClassroomTypeEvaluator : IConstraintEvaluator
{
    public string Code => "H4";
    public string Name => "Derslik Tipi Uyumu";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var config = context.Config;
        var conflicts = new List<SchedulingConflictDto>();

        // Lab dersi Lab/Computer derslikte olmalı — BUG FIX B16: hard conflict
        if (c.ClassroomId.HasValue
            && c.LabHours > 0 && c.TheoryHours == 0
            && c.ClassroomType != ClassroomType.Lab && c.ClassroomType != ClassroomType.Computer)
        {
            conflicts.Add(SchedulingHelpers.BuildConflict(
                c,
                "ClassroomTypeConflict",
                $"Lab section'u ({c.CourseCode}) Lab veya Computer tipinde bir dersliğe atanmalıdır, mevcut tip: {c.ClassroomType}.",
                null,
                null));
        }

        // 2026-05-09 — Simetrik kural: teori bloğu (TheoryHours>0 olan section) lab-kategori
        // dersliklere yerleşmemeli. Lab dersliği scarce kaynak. Encoder ile parity.
        if (c.ClassroomId.HasValue
            && c.TheoryHours > 0 && c.LabHours == 0
            && c.ClassroomType.IsLabCategory())
        {
            conflicts.Add(SchedulingHelpers.BuildConflict(
                c,
                "ClassroomTypeConflict",
                $"Teori dersi ({c.CourseCode}) lab/uzman tipi dersliğe atanamaz, mevcut tip: {c.ClassroomType}.",
                null,
                null));
        }

        // Büyük dersler (kontenjan >= AmphiThreshold) → Amphitheater zorunlu.
        // Plan 11 Faz 1.4 — V2 ClassroomTypeEncoder ile parite: AmphiThresholdEnabled flag-gated
        // (default false). Production'da 0 Amphitheater envanteri olduğu için flag kapalı kalıyor;
        // V2 bu kuralı zaten uygulamıyordu, V1 evaluator ise her zaman uyguluyordu → drift.
        // Bu patch flag'i V1 tarafına da getirir.
        if (config.AmphiThresholdEnabled
            && c.ClassroomId.HasValue
            && c.Capacity >= config.AmphiThreshold
            && c.ClassroomType != ClassroomType.Amphitheater)
        {
            conflicts.Add(SchedulingHelpers.BuildConflict(
                c,
                "ClassroomTypeConflict",
                $"Kontenjanı {c.Capacity} olan ders için Amfi derslik gereklidir.",
                null,
                null));
        }

        return conflicts.Count > 0
            ? ConstraintResult.WithConflicts(conflicts)
            : ConstraintResult.Ok();
    }
}
