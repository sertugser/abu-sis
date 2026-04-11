using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

internal sealed class CreditHourEvaluator : IConstraintEvaluator
{
    public string Code => "H12";
    public string Name => "Kredi-Saat Uyumu";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;

        if (c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        var expectedHours = c.TheoryHours + c.LabHours;
        if (expectedHours <= 0)
            return ConstraintResult.Ok();

        if (c.AssignedTimeSlotCount != expectedHours)
        {
            return ConstraintResult.WithConflict(SchedulingHelpers.BuildConflict(
                c,
                "CreditHourMismatch",
                $"Dersin haftalik slot gereksinimine uygun atama yapilmamis ({c.CourseCode}: beklenen {expectedHours} slot, atanan {c.AssignedTimeSlotCount} slot).",
                null,
                null));
        }

        return ConstraintResult.Ok();
    }
}
