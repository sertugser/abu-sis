using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

internal sealed class CapacityEvaluator : IConstraintEvaluator
{
    public string Code => "H3";
    public string Name => "Kapasite Kontrolü";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;

        if (c.ClassroomId.HasValue && c.ClassroomCapacity < c.Capacity)
        {
            return ConstraintResult.WithConflict(SchedulingHelpers.BuildConflict(
                c,
                "CapacityConflict",
                $"Derslik kapasitesi section kontenjanini karsilamiyor ({c.ClassroomCapacity}/{c.Capacity}).",
                null,
                null));
        }

        return ConstraintResult.Ok();
    }
}
