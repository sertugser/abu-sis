using SIS.Domain.Entities;
using SIS.Domain.Enums;
using System.Linq.Expressions;

namespace SIS.Infrastructure.Services;

internal static class RegistrationRulePolicy
{
    private static readonly Expression<Func<CourseRegistration, bool>> FailedGradeExpression = registration =>
        registration.Grade == GradeCode.FF
        || registration.Grade == GradeCode.NA
        || registration.Grade == GradeCode.U;

    private static readonly Expression<Func<CourseRegistration, bool>> PassedGradeExpression = registration =>
        registration.Grade != null
        && registration.Grade != GradeCode.FF
        && registration.Grade != GradeCode.NA
        && registration.Grade != GradeCode.U
        && registration.Grade != GradeCode.W
        && registration.Grade != GradeCode.I;

    internal static bool IsFailedGrade(GradeCode? grade) =>
        grade is GradeCode.FF or GradeCode.NA or GradeCode.U;

    internal static bool IsPassedGrade(GradeCode? grade) =>
        grade != null
        && grade != GradeCode.FF
        && grade != GradeCode.NA
        && grade != GradeCode.U
        && grade != GradeCode.W
        && grade != GradeCode.I;

    internal static IQueryable<CourseRegistration> WhereFailedGrade(this IQueryable<CourseRegistration> query) =>
        query.Where(FailedGradeExpression);

    internal static IQueryable<CourseRegistration> WherePassedGrade(this IQueryable<CourseRegistration> query) =>
        query.Where(PassedGradeExpression);

    internal static IEnumerable<CourseRegistration> WhereFailedGrade(this IEnumerable<CourseRegistration> source) =>
        source.Where(registration => IsFailedGrade(registration.Grade));

    internal static IEnumerable<CourseRegistration> WherePassedGrade(this IEnumerable<CourseRegistration> source) =>
        source.Where(registration => IsPassedGrade(registration.Grade));

    internal static bool HasLowCgpaRestriction(decimal cgpa, int completedSemesters, decimal retentionThreshold = 1.70m) =>
        cgpa < retentionThreshold && completedSemesters > 1;

    internal static bool IsDemandStatus(RegistrationStatus status) =>
        status is RegistrationStatus.Draft
            or RegistrationStatus.PendingAdvisorApproval
            or RegistrationStatus.Active;

    internal static bool IsAllowedUnderLowCgpaRestriction(
        Course course,
        int? suggestedSemester,
        int completedSemesters,
        int? programDepartmentId,
        bool isFailedCourse,
        bool isDdDcImprovement)
    {
        // Öğrencinin içinde bulunduğu yarıyıl = completedSemesters + 1. Şartlı öğrenci kendi
        // döneminin ve altının derslerini alabilir; yalnızca DAHA üst yarıyıllar kısıtlıdır.
        var currentSemester = completedSemesters + 1;
        var isCurrentOrLowerSemester = suggestedSemester != null && suggestedSemester.Value <= currentSemester;
        var isNonDepartmental = course.DepartmentId != null
            && programDepartmentId != null
            && course.DepartmentId != programDepartmentId;

        return isFailedCourse || isDdDcImprovement || isCurrentOrLowerSemester || isNonDepartmental;
    }

    internal static ConflictOverlapType CheckScheduleOverlap(List<TimeBlock> schedule1, List<TimeBlock> schedule2)
    {
        bool hasPartialOverlap = false;
        bool hasFullOverlap = false;

        foreach (var slot1 in schedule1)
        {
            foreach (var slot2 in schedule2)
            {
                if (slot1.Day != slot2.Day) continue;

                if (slot1.StartTime < slot2.EndTime && slot2.StartTime < slot1.EndTime)
                {
                    if ((slot1.StartTime == slot2.StartTime && slot1.EndTime == slot2.EndTime) ||
                        (slot1.StartTime <= slot2.StartTime && slot1.EndTime >= slot2.EndTime) ||
                        (slot2.StartTime <= slot1.StartTime && slot2.EndTime >= slot1.EndTime))
                    {
                        hasFullOverlap = true;
                    }
                    else
                    {
                        hasPartialOverlap = true;
                    }
                }
            }
        }

        if (hasFullOverlap) return ConflictOverlapType.Full;
        if (hasPartialOverlap) return ConflictOverlapType.Partial;
        return ConflictOverlapType.None;
    }
}
