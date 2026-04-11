using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

internal sealed class CoEnrollmentEvaluator : IConstraintEvaluator
{
    public string Code => "S4";
    public string Name => "Birlikte Alınan Ders Çakışması";
    public ConstraintCategory Category => ConstraintCategory.Soft;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var config = context.Config;
        var snapshot = context.Snapshot;

        if (!config.S4Enabled || c.TimeBlocks.Count == 0 || snapshot.CoEnrollmentPairs.Count == 0)
            return ConstraintResult.Ok();

        var relevantPairs = snapshot.CoEnrollmentPairs
            .Where(p => p.CourseId1 == c.CourseId || p.CourseId2 == c.CourseId)
            .ToList();

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var pair in relevantPairs)
        {
            var pairedCourseId = pair.CourseId1 == c.CourseId ? pair.CourseId2 : pair.CourseId1;
            var pairedSections = snapshot.Sections
                .Where(s => s.CourseId == pairedCourseId && s.TimeBlocks.Count > 0)
                .ToList();

            foreach (var pairedSec in pairedSections)
            {
                var overlaps = SchedulingHelpers.GetOverlapSlots(c.TimeBlocks, pairedSec.TimeBlocks);
                if (overlaps.Count > 0)
                {
                    totalPenalty += pair.StudentCount * 0.5;
                    notes.Add($"S4 Uyarı: '{c.CourseCode}' ve '{pairedSec.CourseCode}' dersleri sık birlikte alınıyor ({pair.StudentCount} öğrenci) ancak zaman çakışması var.");
                }
            }
        }

        if (totalPenalty > 0)
            return new ConstraintResult { SoftPenalty = totalPenalty, Notes = notes };

        return ConstraintResult.Ok();
    }
}
