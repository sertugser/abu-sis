using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

internal sealed class DailyLoadEvaluator : IConstraintEvaluator
{
    public string Code => "H-DL";
    public string Name => "Günlük Ders Yükü (9 saat)";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;

        if (!c.InstructorId.HasValue || c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        var instructorSections = context.Snapshot.Sections
            .Where(s => s.InstructorId == c.InstructorId && s.TimeBlocks.Count > 0)
            .ToList();

        var dailyGroups = instructorSections
            .SelectMany(s => s.TimeBlocks.Select(slot => new { slot.Day, slot.StartTime, slot.EndTime }))
            .Concat(c.TimeBlocks.Select(slot => new { slot.Day, slot.StartTime, slot.EndTime }))
            .GroupBy(slot => slot.Day);

        var conflicts = new List<SchedulingConflictDto>();

        foreach (var dayGroup in dailyGroups)
        {
            var dailyHours = dayGroup.Sum(slot => (slot.EndTime - slot.StartTime).TotalHours);
            if (dailyHours <= 9)
                continue;

            var dayName = SchedulingHelpers.GetDayOfWeekName(dayGroup.Key);
            conflicts.Add(SchedulingHelpers.BuildConflict(
                c,
                "DailyLoadConflict",
                $"Öğretim üyesi '{c.InstructorName}' {dayName} günü {dailyHours:0.#} saat ders veriyor (maks 9 saat).",
                null,
                null));
        }

        return conflicts.Count > 0
            ? ConstraintResult.WithConflicts(conflicts)
            : ConstraintResult.Ok();
    }
}
