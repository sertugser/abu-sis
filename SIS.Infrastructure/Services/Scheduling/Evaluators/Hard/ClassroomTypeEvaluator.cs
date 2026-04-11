using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

internal sealed class ClassroomTypeEvaluator : IConstraintEvaluator
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

        // Büyük dersler (kontenjan >= AmphiThreshold) → Amphitheater zorunlu
        if (c.ClassroomId.HasValue
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
