using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

public sealed class TheoryLabDaySplitEvaluator : IConstraintEvaluator
{
    public string Code => "H16";
    public string Name => "Teori-Lab Gun Ayirimi";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var threshold = context.Config.TheoryLabSplitThreshold;

        // Not a mixed course (must have both theory and lab hours)
        if (c.TheoryHours <= 0 || c.LabHours <= 0)
            return ConstraintResult.Ok();

        // Constraint disabled
        if (threshold <= 0)
            return ConstraintResult.Ok();

        // Under or at threshold
        if (c.TheoryHours + c.LabHours <= threshold)
            return ConstraintResult.Ok();

        // Not scheduled yet
        if (c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        var distinctDays = c.TimeBlocks.Select(block => block.Day).Distinct().Count();

        // Already on multiple days — constraint satisfied
        if (distinctDays > 1)
            return ConstraintResult.Ok();

        // All blocks on the same day AND total > threshold
        return ConstraintResult.WithConflict(SchedulingHelpers.BuildConflict(
            c,
            "TheoryLabDaySplitConflict",
            $"Karma ders '{c.CourseCode}' ({c.TheoryHours}T+{c.LabHours}L={c.TheoryHours + c.LabHours} saat) esik degeri ({threshold}) asti; " +
            $"teori ve lab bloklari farkli gunlere atanmalidir.",
            null,
            null));
    }
}
