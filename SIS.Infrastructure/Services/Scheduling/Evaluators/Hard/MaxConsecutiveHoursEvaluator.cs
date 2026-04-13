using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

public sealed class MaxConsecutiveHoursEvaluator : IConstraintEvaluator
{
    public string Code => "H15";
    public string Name => "Maksimum Ardışık Ders Saati";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var config = context.Config;

        if (!c.InstructorId.HasValue || c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        var existingSlots = context.Snapshot.Sections
            .Where(s => s.InstructorId == c.InstructorId)
            .SelectMany(s => s.TimeBlocks.Select(slot => new { Slot = slot }))
            .ToList();

        var candidateSlots = c.TimeBlocks
            .Select(slot => new { Slot = slot })
            .ToList();

        var allSlots = existingSlots.Concat(candidateSlots);
        var conflicts = new List<SchedulingConflictDto>();

        foreach (var dayGroup in allSlots.GroupBy(x => x.Slot.Day))
        {
            var sortedSlots = dayGroup.OrderBy(x => x.Slot.StartTime).ToList();
            if (sortedSlots.Count <= 1)
                continue;

            var blockMinutes = (sortedSlots[0].Slot.EndTime - sortedSlots[0].Slot.StartTime).TotalMinutes;
            var maxBlockMinutes = blockMinutes;

            for (int idx = 1; idx < sortedSlots.Count; idx++)
            {
                var gap = (sortedSlots[idx].Slot.StartTime - sortedSlots[idx - 1].Slot.EndTime).TotalMinutes;
                if (gap <= config.BreakDurationMinutes)
                {
                    blockMinutes += (sortedSlots[idx].Slot.EndTime - sortedSlots[idx].Slot.StartTime).TotalMinutes;
                }
                else
                {
                    blockMinutes = (sortedSlots[idx].Slot.EndTime - sortedSlots[idx].Slot.StartTime).TotalMinutes;
                }

                if (blockMinutes > maxBlockMinutes)
                    maxBlockMinutes = blockMinutes;
            }

            var blockHours = maxBlockMinutes / 60.0;
            if (blockHours > config.MaxConsecutiveHours)
            {
                var dayName = SchedulingHelpers.GetDayOfWeekName(dayGroup.Key);
                conflicts.Add(SchedulingHelpers.BuildConflict(
                    c,
                    "MaxConsecutiveHoursConflict",
                    $"Öğretim üyesi '{c.InstructorName}' {dayName} günü {config.MaxConsecutiveHours} saatten fazla aralıksız ders veriyor ({blockHours:0.#} saat).",
                    null,
                    null));
            }
        }

        return conflicts.Count > 0
            ? ConstraintResult.WithConflicts(conflicts)
            : ConstraintResult.Ok();
    }
}
