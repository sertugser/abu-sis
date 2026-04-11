using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

public sealed class DailyLoadSoftEvaluator : IConstraintEvaluator
{
    public string Code => "S2";
    public string Name => "Günlük Yoğunluk Dengesi";
    public ConstraintCategory Category => ConstraintCategory.Soft;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var config = context.Config;

        if (!config.S2Enabled || !c.InstructorId.HasValue || c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        var existingSlots = context.Snapshot.Sections
            .Where(s => s.InstructorId == c.InstructorId)
            .SelectMany(s => s.TimeBlocks)
            .ToList();

        var allSlots = existingSlots.Concat(c.TimeBlocks);

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var dayGroup in allSlots.GroupBy(slot => slot.Day))
        {
            var dailyHours = dayGroup.Sum(slot => (slot.EndTime - slot.StartTime).TotalHours);
            if (dailyHours > config.S2MaxDailyHours)
            {
                totalPenalty += (dailyHours - config.S2MaxDailyHours) * 10;
                var dayName = SchedulingHelpers.GetDayOfWeekName(dayGroup.Key);
                notes.Add($"S2 Uyarı: {c.InstructorName} hocanın {dayName} günündeki ders yükü fazla ({dailyHours:0.#} saat, max {config.S2MaxDailyHours})");
            }
        }

        if (totalPenalty > 0)
            return new ConstraintResult { SoftPenalty = totalPenalty, Notes = notes };

        return ConstraintResult.Ok();
    }
}
