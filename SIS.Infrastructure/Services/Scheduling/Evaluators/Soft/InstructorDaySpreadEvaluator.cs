using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

internal sealed class InstructorDaySpreadEvaluator : IConstraintEvaluator
{
    public string Code => "S8";
    public string Name => "Öğretim Üyesi Gün Dağılımı";
    public ConstraintCategory Category => ConstraintCategory.Soft;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var config = context.Config;

        if (!config.S8Enabled || !c.InstructorId.HasValue || c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        var existingSlots = context.Snapshot.Sections
            .Where(s => s.InstructorId == c.InstructorId)
            .SelectMany(s => s.TimeBlocks)
            .ToList();

        var allSlots = existingSlots.Concat(c.TimeBlocks).ToList();
        var usedDays = allSlots.Select(s => s.Day).Distinct().Count();
        var totalHours = allSlots.Sum(s => (s.EndTime - s.StartTime).TotalHours);
        var maxHoursPerDay = (double)config.S2MaxDailyHours;
        var minPossibleDays = Math.Max(1, (int)Math.Ceiling(totalHours / maxHoursPerDay));

        // Penalty calculation uses minPossibleDays (not minPossibleDays + 1 like note)
        double penalty = 0;
        string? note = null;

        if (usedDays > minPossibleDays)
            penalty = (usedDays - minPossibleDays) * 4;

        if (usedDays > minPossibleDays + 1)
            note = $"S8 Uyarı: {c.InstructorName} hocanın dersleri {usedDays} güne dağılmış (minimum {minPossibleDays} gün yeterli olabilir).";

        if (penalty > 0)
            return ConstraintResult.WithPenalty(penalty, note);

        return ConstraintResult.Ok();
    }
}
