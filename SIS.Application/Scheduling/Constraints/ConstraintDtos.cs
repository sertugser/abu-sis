namespace SIS.Application.Scheduling.Constraints;

public record ConstraintDefinitionDto(
    int Id, string Code, string Name, string? Description,
    string Category, string Scope, bool IsEnabled,
    string? Parameters, int SortOrder, int ExceptionCount);

public record ConstraintDefinitionDetailDto(
    int Id, string Code, string Name, string? Description,
    string Category, string Scope, bool IsEnabled,
    string? Parameters, int SortOrder,
    List<ConstraintExceptionDto> Exceptions);

public record ConstraintExceptionDto(
    int Id, int ConstraintDefinitionId, string ConstraintCode,
    int? InstructorId, string? InstructorName,
    int? StudentId, string? StudentName,
    int? DepartmentId, string? DepartmentName,
    int? CourseSectionId, string? CourseSectionInfo,
    int? TermId, string? TermName,
    bool IsExempt, string? ParameterOverride,
    string? Reason, DateTime CreatedAt);

public record AddExceptionRequest(
    int? InstructorId, int? StudentId, int? DepartmentId,
    int? CourseSectionId, int? TermId,
    bool IsExempt, string? ParameterOverride, string? Reason);

public record UpdateParametersRequest(string Parameters);
public record UpdateCategoryRequest(string Category);
