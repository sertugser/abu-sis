using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators;

public sealed class DistributionConstraintEvaluator : IConstraintEvaluator
{
    public string Code => "DIST";
    public string Name => "İlişkisel Kısıtlamalar";
    public ConstraintCategory Category => ConstraintCategory.Soft;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var distConstraints = context.Snapshot.DistributionConstraints;

        if (distConstraints == null || distConstraints.Count == 0)
            return ConstraintResult.Ok();

        var conflicts = new List<SchedulingConflictDto>();
        double totalPenalty = 0;

        foreach (var dist in distConstraints)
        {
            var candidateCourse = dist.Courses.FirstOrDefault(dc =>
                dc.CourseId == c.CourseId && (dc.CourseSectionId == null || dc.CourseSectionId == c.SectionId));

            if (candidateCourse == null) continue;

            var otherCourses = dist.Courses
                .Where(dc => dc.CourseId != c.CourseId || (dc.CourseSectionId != null && dc.CourseSectionId != c.SectionId))
                .ToList();

            foreach (var otherDc in otherCourses)
            {
                var otherSections = context.Snapshot.Sections
                    .Where(s => s.CourseId == otherDc.CourseId &&
                        (otherDc.CourseSectionId == null || s.SectionId == otherDc.CourseSectionId))
                    .ToList();

                foreach (var other in otherSections)
                {
                    if (other.TimeBlocks.Count == 0 || c.TimeBlocks.Count == 0)
                        continue;

                    var violated = CheckViolation(dist.DistributionType, c, other, candidateCourse, otherDc);
                    if (!violated) continue;

                    var isHard = dist.PreferenceLevel is PreferenceLevel.Unavailable;
                    var message = $"DIST: {dist.DistributionType} ihlali — {c.CourseCode} ↔ {other.CourseCode}";

                    if (isHard)
                    {
                        conflicts.Add(SchedulingHelpers.BuildConflict(
                            c, "DistributionConstraint", message, c.TimeBlocks.FirstOrDefault(), other.SectionId));
                    }
                    else
                    {
                        var penalty = dist.PreferenceLevel switch
                        {
                            PreferenceLevel.StronglyDiscouraged => 30,
                            PreferenceLevel.Discouraged => 10,
                            _ => 5,
                        };
                        totalPenalty += penalty;
                    }
                }
            }
        }

        if (conflicts.Count > 0)
            return ConstraintResult.WithConflicts(conflicts);
        if (totalPenalty > 0)
            return ConstraintResult.WithPenalty(totalPenalty, $"DIST: İlişkisel kısıtlama ihlalleri (ceza: {totalPenalty})");

        return ConstraintResult.Ok();
    }

    private static bool CheckViolation(
        DistributionType type,
        SectionSnapshot a,
        SectionSnapshot b,
        DistributionConstraintCourse aCourse,
        DistributionConstraintCourse bCourse)
    {
        switch (type)
        {
            case DistributionType.SameDay:
            {
                var aDays = a.TimeBlocks.Select(t => t.Day).Distinct();
                var bDays = b.TimeBlocks.Select(t => t.Day).Distinct();
                return !aDays.Intersect(bDays).Any();
            }

            case DistributionType.DifferentDay:
            {
                var aDays = a.TimeBlocks.Select(t => t.Day).Distinct();
                var bDays = b.TimeBlocks.Select(t => t.Day).Distinct();
                return aDays.Intersect(bDays).Any();
            }

            case DistributionType.SameRoom:
                return a.ClassroomId != b.ClassroomId;

            case DistributionType.BackToBack:
                return !AreBackToBack(a, b);

            case DistributionType.NotBackToBack:
                return AreBackToBack(a, b);

            case DistributionType.Precedence:
            {
                // a should come before b (based on SortOrder)
                if (aCourse.SortOrder >= bCourse.SortOrder) return false;
                var aLast = a.TimeBlocks.Max(t => (int)t.Day * 1440 + (int)t.EndTime.ToTimeSpan().TotalMinutes);
                var bFirst = b.TimeBlocks.Min(t => (int)t.Day * 1440 + (int)t.StartTime.ToTimeSpan().TotalMinutes);
                return aLast > bFirst;
            }

            default:
                return false;
        }
    }

    private static bool AreBackToBack(SectionSnapshot a, SectionSnapshot b)
    {
        foreach (var aBlock in a.TimeBlocks)
        {
            foreach (var bBlock in b.TimeBlocks)
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
