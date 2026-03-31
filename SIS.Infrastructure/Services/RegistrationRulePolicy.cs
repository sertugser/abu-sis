using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;

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

    /// <summary>
    /// CGPA'ya göre ECTS limitini döner (hardcoded fallback).
    /// Mümkün olan her yerde GetEctsLimitAsync kullanılmalıdır.
    /// </summary>
    internal static int GetEctsLimit(decimal cgpa) =>
        cgpa >= 2.50m ? 48 :
        cgpa >= 2.00m ? 42 : 30;

    /// <summary>
    /// CGPA'ya göre ECTS limitini ISystemConfigService üzerinden (DB'den önbellekli) döner.
    /// </summary>
    internal static Task<int> GetEctsLimitAsync(decimal cgpa, ISystemConfigService config,
        CancellationToken cancellationToken = default)
        => config.GetEctsLimitAsync(cgpa, cancellationToken);

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

    internal static ConflictOverlapType CheckScheduleOverlap(List<ScheduleSlot> schedule1, List<ScheduleSlot> schedule2)
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
