using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

internal static class RegistrationRulePolicy
{
    internal static bool IsFailedGrade(GradeCode? grade) =>
        grade is GradeCode.FF or GradeCode.NA or GradeCode.U;

    internal static bool IsPassedGrade(GradeCode? grade) =>
        grade != null
        && grade != GradeCode.FF
        && grade != GradeCode.NA
        && grade != GradeCode.U
        && grade != GradeCode.W
        && grade != GradeCode.I;

    internal static int GetEctsLimit(decimal cgpa) =>
        cgpa >= 2.50m ? 48 :
        cgpa >= 2.00m ? 42 : 30;

    internal static bool HasLowCgpaRestriction(decimal cgpa, int completedSemesters) =>
        cgpa < 1.70m && completedSemesters > 1;

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
        var isLowerOrSameSemester = suggestedSemester != null && suggestedSemester.Value <= completedSemesters;
        var isNonDepartmental = course.DepartmentId != null
            && programDepartmentId != null
            && course.DepartmentId != programDepartmentId;

        return isFailedCourse || isDdDcImprovement || isLowerOrSameSemester || isNonDepartmental;
    }
}
