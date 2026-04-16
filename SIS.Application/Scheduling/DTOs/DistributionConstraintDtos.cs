namespace SIS.Application.Scheduling.DTOs;

public sealed record DistributionConstraintDto(
    int Id,
    string DistributionType,
    string PreferenceLevel,
    int TermId,
    int? DepartmentId,
    string? DepartmentName,
    string? Reason,
    DateTime CreatedAt,
    IReadOnlyList<DistributionConstraintCourseDto> Courses);

public sealed record DistributionConstraintCourseDto(
    int Id,
    int CourseId,
    string CourseCode,
    string CourseName,
    int? CourseSectionId,
    string? SectionCode,
    int SortOrder);

public sealed record CreateDistributionConstraintRequest(
    string DistributionType,
    string PreferenceLevel,
    int TermId,
    int? DepartmentId,
    string? Reason,
    List<CreateDistributionCourseItem> Courses);

public sealed record CreateDistributionCourseItem(
    int CourseId,
    int? CourseSectionId,
    int SortOrder);
