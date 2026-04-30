namespace SIS.Application.Common.DTOs;

public record CurriculumCourseOpeningReasonDto(
    int Id, string Code, string Name, string? Description,
    int SortOrder, bool IsActive,
    // Behavior flags exposed for future client-side hints (UI badges, tooltips)
    bool AllowsAdvisorGrading,
    bool BypassScheduleConflict,
    bool BypassMandatoryAttendance,
    bool IsExcludedFromGpa,
    bool IsCreditFree,
    bool RequiresApproval,
    bool AllowsCustomGradingScale);

public record CourseCategoryDto(
    int Id, string Code, string Name, int SortOrder, bool IsActive);
