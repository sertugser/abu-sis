using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

public sealed class CourseComponentEvaluator : IConstraintEvaluator
{
    public string Code => "H6";
    public string Name => "Ders Bileşen Çakışması";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var conflicts = new List<SchedulingConflictDto>();

        var candidateIsLab = c.LabHours > 0 && c.TheoryHours == 0;
        var candidateIsLecture = c.TheoryHours > 0;

        foreach (var existing in context.Snapshot.Sections)
        {
            if (c.CourseId != existing.CourseId)
                continue;

            var existingIsLab = existing.LabHours > 0 && existing.TheoryHours == 0;
            var existingIsLecture = existing.TheoryHours > 0;

            if (!((candidateIsLab && existingIsLecture) || (candidateIsLecture && existingIsLab)))
                continue;

            foreach (var overlap in SchedulingHelpers.GetOverlapSlots(c.TimeBlocks, existing.TimeBlocks))
            {
                conflicts.Add(SchedulingHelpers.BuildConflict(
                    c,
                    "SameCourseComponentConflict",
                    $"Aynı dersin ({c.CourseCode}) lecture ve lab bileşenleri aynı zaman aralığında çakışıyor.",
                    overlap,
                    existing.SectionId));
            }
        }

        return conflicts.Count > 0
            ? ConstraintResult.WithConflicts(conflicts)
            : ConstraintResult.Ok();
    }
}
