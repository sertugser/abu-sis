using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

public sealed class SlotCongestionEvaluator : IConstraintEvaluator
{
    public string Code => "S3";
    public string Name => "Slot Yığılması";
    public ConstraintCategory Category => ConstraintCategory.Soft;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var config = context.Config;

        if (!config.S3Enabled || c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        var existingSlotKeys = context.Snapshot.Sections
            .SelectMany(s => s.TimeBlocks.Select(slot => (slot.Day, slot.StartTime)))
            .ToList();

        var candidateSlotKeys = c.TimeBlocks
            .Select(slot => (slot.Day, slot.StartTime))
            .ToList();

        var allSlotKeys = existingSlotKeys.Concat(candidateSlotKeys);

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var group in allSlotKeys.GroupBy(x => x))
        {
            var count = group.Count();
            if (count > config.S3MaxSectionsPerSlot && candidateSlotKeys.Contains(group.Key))
            {
                totalPenalty += (count - config.S3MaxSectionsPerSlot) * 5;
                var dayName = SchedulingHelpers.GetDayOfWeekName(group.Key.Day);
                notes.Add($"S3 Uyarı: {dayName} {group.Key.StartTime:HH\\:mm} slotunda {count} section yığılması var (max {config.S3MaxSectionsPerSlot})");
            }
        }

        if (totalPenalty > 0)
            return new ConstraintResult { SoftPenalty = totalPenalty, Notes = notes };

        return ConstraintResult.Ok();
    }
}
