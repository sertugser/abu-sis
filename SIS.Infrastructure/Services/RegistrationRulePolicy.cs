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

    internal static int GetEctsLimit(decimal cgpa, int limitBelow200 = 30, int limit200To249 = 42, int limitAbove250 = 48) =>
        cgpa >= 2.50m ? limitAbove250 :
        cgpa >= 2.00m ? limit200To249 : limitBelow200;

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
        var isLowerOrSameSemester = suggestedSemester != null && suggestedSemester.Value <= completedSemesters;
        var isNonDepartmental = course.DepartmentId != null
            && programDepartmentId != null
            && course.DepartmentId != programDepartmentId;

        return isFailedCourse || isDdDcImprovement || isLowerOrSameSemester || isNonDepartmental;
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
