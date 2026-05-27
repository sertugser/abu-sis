using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hybrid;

/// <summary>
/// Plan 12 Faz 2.10 — DIST İlişkisel Kısıtlamalar (V1 <c>DistributionConstraintEvaluator</c> portu).
///
/// HYBRID predicate: hem <see cref="IRulePredicate.Evaluate"/> (hard violation, PreferenceLevel=Unavailable)
/// hem <see cref="ISoftRulePredicate.CalculatePenalty"/> (diğer level'lar için 30/10/5 penalty) döndürür.
/// RuleEngine Plan 10 Category toggle'a göre ya hard ya soft dispatch yapar; predicate'in iki tarafı
/// da snapshot'ı taradığından her iki yola katkı verir.
///
/// V1 mantığı korunur: DistributionType (SameDay/DifferentDay/SameRoom/BackToBack/NotBackToBack/Precedence)
/// per-pair check; Unavailable → hard, StronglyDiscouraged→30 / Discouraged→10 / diğer→5 penalty.
/// </summary>
public sealed class DIST_DistributionRule : ISoftRulePredicate
{
    public string Code => "DIST";
    public RuleCategory DefaultCategory => RuleCategory.Soft;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var snapshot = ctx.Snapshot;
        if (snapshot.DistributionConstraints.Count == 0 || c.Blocks.Count == 0)
            return Array.Empty<RuleViolation>();

        var violations = new List<RuleViolation>();

        foreach (var dist in snapshot.DistributionConstraints)
        {
            if (dist.PreferenceLevel != PreferenceLevel.Unavailable) continue;

            foreach (var (candCourse, otherCourse, other) in FindViolatingPairs(c, dist, snapshot))
            {
                var firstBlock = c.Blocks[0];
                violations.Add(new RuleViolation(
                    Code: Code, ConflictType: "DistributionConstraint",
                    Message: $"DIST: {dist.DistributionType} ihlali — {c.CourseCode} ↔ {other.CourseCode}",
                    SectionId: c.SectionId, RelatedSectionId: other.SectionId,
                    Day: firstBlock.Day,
                    StartTime: firstBlock.StartTime, EndTime: firstBlock.EndTime));
            }
        }

        return violations;
    }

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var snapshot = ctx.Snapshot;
        if (snapshot.DistributionConstraints.Count == 0 || c.Blocks.Count == 0)
            return SoftRuleResult.Zero;

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var dist in snapshot.DistributionConstraints)
        {
            if (dist.PreferenceLevel == PreferenceLevel.Unavailable) continue;

            // Ceza ağırlığı config'ten (encoder ile TEK kaynak). Yalnız Discouraged/StronglyDiscouraged
            // cezalandırılır — solver encoder'ı da Preferred/StronglyPreferred'ı modellemiyor (skip),
            // bu yüzden evaluator da onları cezalandırmaz (eski "_ => 5.0" drift'i kaldırıldı).
            double penalty = dist.PreferenceLevel switch
            {
                PreferenceLevel.StronglyDiscouraged => ctx.Config.DistStronglyDiscouragedWeight,
                PreferenceLevel.Discouraged => ctx.Config.DistDiscouragedWeight,
                _ => 0.0,
            };
            if (penalty <= 0) continue;

            foreach (var (candCourse, otherCourse, other) in FindViolatingPairs(c, dist, snapshot))
            {
                totalPenalty += penalty;
                notes.Add($"DIST: {dist.DistributionType} ihlali — {c.CourseCode} ↔ {other.CourseCode} (penalty {penalty})");
            }
        }

        return totalPenalty > 0
            ? new SoftRuleResult(totalPenalty, notes.Count, 1, notes)
            : SoftRuleResult.Zero;
    }

    // Candidate'ın ait olduğu her DistributionConstraint için pair yardımcıları döndürür.
    private static IEnumerable<(DistributionConstraintCourseInfo candCourse, DistributionConstraintCourseInfo otherCourse, RuntimeCandidate otherSection)>
        FindViolatingPairs(RuntimeCandidate c, RuntimeDistributionConstraint dist, RuntimeSnapshot snapshot)
    {
        var candCourse = dist.Courses.FirstOrDefault(dc =>
            dc.CourseId == c.CourseId && (dc.CourseSectionId == null || dc.CourseSectionId == c.SectionId));
        if (candCourse == null) yield break;

        var otherCourses = dist.Courses
            .Where(dc => dc.CourseId != c.CourseId || (dc.CourseSectionId != null && dc.CourseSectionId != c.SectionId))
            .ToList();

        foreach (var otherDc in otherCourses)
        {
            var otherSections = snapshot.Sections
                .Where(s => s.CourseId == otherDc.CourseId
                    && (otherDc.CourseSectionId == null || s.SectionId == otherDc.CourseSectionId))
                .ToList();

            foreach (var other in otherSections)
            {
                if (other.Blocks.Count == 0) continue;
                if (other.SectionId == c.SectionId) continue;
                if (!CheckViolation(dist.DistributionType, c, other, candCourse, otherDc)) continue;
                yield return (candCourse, otherDc, other);
            }
        }
    }

    private static bool CheckViolation(
        DistributionType type,
        RuntimeCandidate a,
        RuntimeCandidate b,
        DistributionConstraintCourseInfo aCourse,
        DistributionConstraintCourseInfo bCourse)
    {
        switch (type)
        {
            case DistributionType.SameDay:
            {
                var aDays = a.Blocks.Select(t => t.Day).Distinct();
                var bDays = b.Blocks.Select(t => t.Day).Distinct();
                return !aDays.Intersect(bDays).Any();
            }
            case DistributionType.DifferentDay:
            {
                var aDays = a.Blocks.Select(t => t.Day).Distinct();
                var bDays = b.Blocks.Select(t => t.Day).Distinct();
                return aDays.Intersect(bDays).Any();
            }
            case DistributionType.SameRoom:
                // V1: a.ClassroomId vs b.ClassroomId (per-section). RuntimeCandidate'ta block-level
                // ClassroomId — primary classroom'u ilk bloğun classroomId'sinden al.
                return a.Blocks[0].ClassroomId != b.Blocks[0].ClassroomId;
            case DistributionType.BackToBack:
                return !AreBackToBack(a, b);
            case DistributionType.NotBackToBack:
                return AreBackToBack(a, b);
            case DistributionType.Precedence:
            {
                if (aCourse.SortOrder >= bCourse.SortOrder) return false;
                var aLast = a.Blocks.Max(t => t.Day * 1440 + (int)t.EndTime.ToTimeSpan().TotalMinutes);
                var bFirst = b.Blocks.Min(t => t.Day * 1440 + (int)t.StartTime.ToTimeSpan().TotalMinutes);
                return aLast > bFirst;
            }
            default:
                return false;
        }
    }

    private static bool AreBackToBack(RuntimeCandidate a, RuntimeCandidate b)
    {
        foreach (var aBlock in a.Blocks)
        {
            foreach (var bBlock in b.Blocks)
            {
                if (aBlock.Day != bBlock.Day) continue;
                var gap1 = Math.Abs((int)(bBlock.StartTime.ToTimeSpan() - aBlock.EndTime.ToTimeSpan()).TotalMinutes);
                if (gap1 <= 15) return true;
                var gap2 = Math.Abs((int)(aBlock.StartTime.ToTimeSpan() - bBlock.EndTime.ToTimeSpan()).TotalMinutes);
                if (gap2 <= 15) return true;
            }
        }
        return false;
    }
}
