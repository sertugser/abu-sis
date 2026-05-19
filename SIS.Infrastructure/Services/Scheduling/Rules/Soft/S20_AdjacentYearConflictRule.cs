using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Soft;

/// <summary>
/// Plan 12 Faz 2.9 — S20 Komşu Yıl Çakışma SOFT Penalty (V1 <c>AdjacentYearConflictEvaluator</c> portu).
///
/// V1 mantığı: snapshot.AdjacentYearSoftPairs içinde candidate ile çakışan section çiftleri için
/// pair başına <c>S20PenaltyPerPair</c>.
/// </summary>
public sealed class S20_AdjacentYearConflictRule : ISoftRulePredicate
{
    public string Code => "S20";
    public RuleCategory DefaultCategory => RuleCategory.Soft;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx) => Array.Empty<RuleViolation>();

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var config = ctx.Config;
        var snapshot = ctx.Snapshot;

        if (!config.S20Enabled || c.Blocks.Count == 0) return SoftRuleResult.Zero;
        if (snapshot.AdjacentYearSoftPairs.Count == 0) return SoftRuleResult.Zero;

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var existing in snapshot.Sections)
        {
            if (existing.SectionId == c.SectionId) continue;
            if (existing.Blocks.Count == 0) continue;

            var pair = c.SectionId < existing.SectionId
                ? (c.SectionId, existing.SectionId)
                : (existing.SectionId, c.SectionId);

            if (!snapshot.AdjacentYearSoftPairs.Contains(pair)) continue;
            if (!HasOverlap(c.Blocks, existing.Blocks)) continue;

            totalPenalty += config.S20PenaltyPerPair;
            notes.Add(
                $"S20: '{c.CourseCode}' (yıl modulo eşi '{existing.CourseCode}') ile " +
                $"komşu yıl pair'i çakışıyor — kalan/önden alan öğrenciler için tercih edilmez.");
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
