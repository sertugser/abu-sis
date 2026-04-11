using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

internal sealed class OccupancyEvaluator : IConstraintEvaluator
{
    public string Code => "S1";
    public string Name => "Derslik Doluluk Oranı";
    public ConstraintCategory Category => ConstraintCategory.Soft;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var config = context.Config;

        if (!config.S1Enabled)
            return ConstraintResult.Ok();

        if (!c.ClassroomId.HasValue || c.ClassroomCapacity <= 0)
            return ConstraintResult.Ok();

        var occupancyRate = c.Capacity * 100 / c.ClassroomCapacity;
        var occupancyDouble = c.Capacity * 100.0 / c.ClassroomCapacity;

        if (occupancyRate < config.S1MinOccupancy)
        {
            var penalty = (config.S1MinOccupancy - occupancyDouble) * 0.5;
            return ConstraintResult.WithPenalty(penalty,
                $"S1 Uyarı: {c.CourseCode} ({c.ClassroomName}) doluluk oranı düşük (%{occupancyRate}, hedef >=%{config.S1MinOccupancy})");
        }

        if (occupancyRate > config.S1MaxOccupancy)
        {
            var penalty = (occupancyDouble - config.S1MaxOccupancy) * 0.5;
            return ConstraintResult.WithPenalty(penalty,
                $"S1 Uyarı: {c.CourseCode} ({c.ClassroomName}) doluluk oranı yüksek (%{occupancyRate}, hedef <=%{config.S1MaxOccupancy})");
        }

        return ConstraintResult.Ok();
    }
}
