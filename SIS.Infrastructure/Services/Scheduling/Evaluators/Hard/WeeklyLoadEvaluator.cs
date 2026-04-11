using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

public sealed class WeeklyLoadEvaluator : IConstraintEvaluator
{
    public string Code => "H-WL";
    public string Name => "Haftalık Ders Yükü";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;

        if (!c.InstructorId.HasValue || c.InstructorMaxWeeklyHours <= 0)
            return ConstraintResult.Ok();

        var totalWeeklyHours = context.Snapshot.Sections
            .Where(s => s.InstructorId == c.InstructorId)
            .Sum(s => SchedulingHelpers.ResolveWeeklyHours(s.TimeBlocks, s))
            + SchedulingHelpers.ResolveWeeklyHours(c.TimeBlocks, c);

        if (totalWeeklyHours > c.InstructorMaxWeeklyHours)
        {
            return ConstraintResult.WithConflict(SchedulingHelpers.BuildConflict(
                c,
                "WeeklyLoadConflict",
                $"Instructor haftalik ders yuku limiti asiliyor ({totalWeeklyHours:0.##}/{c.InstructorMaxWeeklyHours} saat).",
                null,
                null));
        }

        return ConstraintResult.Ok();
    }
}
