using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

public sealed class EarlyLateEvaluator : IConstraintEvaluator
{
    public string Code => "S9";
    public string Name => "Erken Sabah / Geç Akşam";
    public ConstraintCategory Category => ConstraintCategory.Soft;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var config = context.Config;

        if (!config.S9Enabled || c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var slot in c.TimeBlocks)
        {
            if (slot.StartTime <= config.S9MorningStart)
            {
                totalPenalty += 6;
                notes.Add($"S9 Uyarı: {c.CourseCode} erken sabah slotuna ({slot.StartTime:HH\\:mm}) programlanmış.");
            }
            else if (slot.StartTime >= config.S9EveningStart)
            {
                totalPenalty += 6;
                notes.Add($"S9 Uyarı: {c.CourseCode} geç akşam slotuna ({slot.StartTime:HH\\:mm}) programlanmış.");
            }
        }

        if (totalPenalty > 0)
            return new ConstraintResult { SoftPenalty = totalPenalty, Notes = notes };

        return ConstraintResult.Ok();
    }
}
