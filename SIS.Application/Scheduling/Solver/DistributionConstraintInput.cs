using SIS.Domain.Enums;

namespace SIS.Application.Scheduling.Solver;

/// <summary>
/// Plan 6 Task 10 — DistributionConstraint Solver input. Mapped from Domain.Entities.DistributionConstraint.
/// 6 distribution type (SameDay/DifferentDay/SameRoom/BackToBack/NotBackToBack/Precedence).
/// 2 tier (Hard=Unavailable, Soft=Discouraged/StronglyDiscouraged).
/// Plan 6 status: infrastructure only (DB read + DTO carry); encoder stub registers diagnostic
/// assumption per constraint. Real CP-SAT enforcement deferred to Plan 6.5 (DB has 0 rows currently).
/// </summary>
public sealed record DistributionConstraintInput(
    int Id,
    DistributionType DistributionType,
    PreferenceLevel PreferenceLevel,
    IReadOnlyList<DistributionCourseInput> Courses);

public sealed record DistributionCourseInput(
    int CourseId,
    int? CourseSectionId,
    int SortOrder);
