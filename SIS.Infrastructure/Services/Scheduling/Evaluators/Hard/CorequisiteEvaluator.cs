using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

public sealed class CorequisiteEvaluator : IConstraintEvaluator
{
    public string Code => "H11";
    public string Name => "Eşkoşul Uyumu";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var snapshot = context.Snapshot;

        foreach (var coreq in snapshot.Corequisites)
        {
            int? pairedCourseId = null;
            if (coreq.CourseId == c.CourseId)
                pairedCourseId = coreq.RequiredCourseId;
            else if (coreq.RequiredCourseId == c.CourseId)
                pairedCourseId = coreq.CourseId;

            if (!pairedCourseId.HasValue)
                continue;

            var pairedSections = snapshot.Sections
                .Where(s => s.CourseId == pairedCourseId.Value && s.TimeBlocks.Count > 0)
                .ToList();

            if (pairedSections.Count == 0)
                continue;

            // Get all sections of candidate's course (existing + the candidate itself)
            var allCandidateCourseSections = snapshot.Sections
                .Where(s => s.CourseId == c.CourseId && s.TimeBlocks.Count > 0 && s.SectionId != c.SectionId)
                .Append(c)
                .Where(s => s.TimeBlocks.Count > 0)
                .ToList();

            var hasNonConflicting = allCandidateCourseSections.Any(a =>
                pairedSections.Any(b =>
                    SchedulingHelpers.GetOverlapSlots(a.TimeBlocks, b.TimeBlocks).Count == 0));

            if (!hasNonConflicting)
            {
                var pairedCode = pairedSections[0].CourseCode;
                return ConstraintResult.WithConflict(SchedulingHelpers.BuildConflict(
                    c,
                    "CorequisiteConflict",
                    $"Corequisite dersler ({c.CourseCode} ve {pairedCode}) arasında çakışmasız bir şube kombinasyonu bulunmuyor.",
                    null,
                    null));
            }
        }

        return ConstraintResult.Ok();
    }
}
