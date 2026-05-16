using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Soft;

/// <summary>
/// Plan 12 Faz 2.4 — S4 Birlikte Alınan Ders Çakışması (V1 <c>CoEnrollmentEvaluator</c> portu).
/// Penalty: pair başına <c>StudentCount × 0.5</c>.
/// </summary>
public sealed class S4_CoEnrollmentRule : ISoftRulePredicate
{
    public string Code => "S4";
    public RuleCategory DefaultCategory => RuleCategory.Soft;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx) => Array.Empty<RuleViolation>();

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var config = ctx.Config;
        var snapshot = ctx.Snapshot;

        if (!config.S4Enabled || c.Blocks.Count == 0 || snapshot.CoEnrollmentPairs.Count == 0)
            return SoftRuleResult.Zero;

        var relevantPairs = snapshot.CoEnrollmentPairs
            .Where(p => p.FirstCourseId == c.CourseId || p.SecondCourseId == c.CourseId)
            .ToList();

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var pair in relevantPairs)
        {
            var pairedCourseId = pair.FirstCourseId == c.CourseId ? pair.SecondCourseId : pair.FirstCourseId;
            var pairedSections = snapshot.Sections
                .Where(s => s.CourseId == pairedCourseId && s.Blocks.Count > 0)
                .ToList();

            foreach (var pairedSec in pairedSections)
            {
                if (HasOverlap(c.Blocks, pairedSec.Blocks))
                {
                    totalPenalty += pair.StudentCount * 0.5;
                    notes.Add($"S4 Uyarı: '{c.CourseCode}' ve '{pairedSec.CourseCode}' dersleri sık birlikte alınıyor ({pair.StudentCount} öğrenci) ancak zaman çakışması var.");
                }
            }
        }

        return totalPenalty > 0
            ? new SoftRuleResult(totalPenalty, notes.Count, 1, notes)
            : SoftRuleResult.Zero;
    }

    private static bool HasOverlap(IReadOnlyList<RuntimeBlock> a, IReadOnlyList<RuntimeBlock> b)
    {
        foreach (var x in a)
            foreach (var y in b)
                if (x.Day == y.Day && x.StartTime < y.EndTime && x.EndTime > y.StartTime)
                    return true;
        return false;
    }
}
