namespace SIS.Application.Scheduling.Constraints;

public record ConstraintDefinitionDto(
    int Id, string Code, string Name, string? Description,
    string Category, string Scope, bool IsEnabled,
    string? Parameters, int SortOrder, int Weight, int Priority,
    int ExceptionCount);

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
public record UpdateWeightRequest(int Weight);

public sealed record ScoreBreakdownDto(
    int TotalHardViolations,
    double TotalSoftPenalty,
    bool Feasible,
    IReadOnlyList<ConstraintScoreItemDto> Constraints);

public sealed record ConstraintScoreItemDto(
    string Code,
    string Name,
    string Category,
    int Weight,
    int ViolationCount,
    double RawPenalty,
    double WeightedPenalty,
    double Percentage,
    int AffectedSectionCount,
    IReadOnlyList<string> SampleViolations);

// ── Exception Request (Approval Workflow) ─────────────────────
public sealed record ExceptionRequestDto(
    int Id, int ConstraintDefinitionId, string ConstraintCode, string ConstraintName,
    string RequestedByUserId, string? RequestedByName, DateTime RequestedAt, string Status,
    int? InstructorId, string? InstructorName,
    int? StudentId, string? StudentName,
    int? DepartmentId, string? DepartmentName,
    int? CourseSectionId, string? CourseSectionInfo,
    int? TermId, string? TermName,
    bool IsExempt, string? ParameterOverride, string Reason,
    DateTime? ExpiresAt, IReadOnlyList<ApprovalStepDto> Steps);

public sealed record ApprovalStepDto(
    int Id, int StepOrder, string RequiredRole, string Status,
    string? DecidedByUserId, string? DecidedByName, DateTime? DecidedAt, string? Comment);

public sealed record CreateExceptionRequestDto(
    int ConstraintDefinitionId,
    int? InstructorId, int? StudentId, int? DepartmentId,
    int? CourseSectionId, int? TermId,
    bool IsExempt, string? ParameterOverride, string Reason,
    DateTime? ExpiresAt);

public sealed record ApprovalActionDto(string? Comment);
