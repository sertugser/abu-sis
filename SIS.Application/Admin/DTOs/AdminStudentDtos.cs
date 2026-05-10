namespace SIS.Application.Admin.DTOs;

public record AdminStudentListItemDto(
    int Id,
    string StudentNumber,
    string FirstName,
    string LastName,
    string Email,
    decimal Cgpa,
    int CompletedSemesters,
    string Status,
    string ProgramName,
    string ProgramCode,
    string DepartmentName,
    string? AdvisorName
);

public record AdminStudentDetailDto(
    int Id,
    string StudentNumber,
    string FirstName,
    string LastName,
    string Email,
    decimal Cgpa,
    decimal Sgpa,
    int CompletedSemesters,
    int FrozenSemesterCount,
    string Status,
    bool IsScholarshipStudent,
    bool HasFinancialBlock,
    string ProgramName,
    string ProgramCode,
    string DepartmentName,
    string? AdvisorName,
    int? AdvisorId
);

public record AdminStudentFilterDto(
    string? Search,
    int? ProgramId,
    int? DepartmentId,
    string? Status,
    decimal? MinCgpa,
    decimal? MaxCgpa,
    int Page = 1,
    int PageSize = 25
);

public record CreateStudentDto(
    string FirstName,
    string LastName,
    string NationalId,
    int ProgramId,
    int EntryYear,
    bool IsScholarshipStudent,
    string Password,
    int? AdvisorId,
    string? OverrideStudentNumber,
    string? OverrideEmail
);
public record BulkAdvisorAssignmentPreviewRequestDto(
    int DepartmentId,
    int? ProgramId,
    bool IncludeStudentsWithAdvisor = false,
    int Page = 1,
    int PageSize = 50,
    string? StudentSearch = null,
    string? AdvisorFilterType = null,
    int? AdvisorFilterId = null
);

public record BulkAdvisorAssignmentApplyRequestDto(
    List<BulkAdvisorAssignmentApplyItemDto> Assignments
);

public record BulkAdvisorAssignmentApplyItemDto(
    int StudentId,
    int AdvisorId
);

public record BulkAdvisorAssignmentPreviewResponseDto(
    int DepartmentId,
    string DepartmentName,
    int? ProgramId,
    string? ProgramName,
    int TotalStudentCount,
    int PlannedAssignmentCount,
    int AlreadyAssignedCount,
    int Page,
    int PageSize,
    int TotalAssignmentCount,
    int TotalPages,
    IReadOnlyList<BulkAdvisorCandidateDto> Advisors,
    IReadOnlyList<BulkAdvisorAssignmentPreviewItemDto> Assignments,
    IReadOnlyList<string> Warnings
);

public record BulkAdvisorCandidateDto(
    int AdvisorId,
    string AdvisorName,
    int DepartmentId,
    string DepartmentName,
    int CurrentStudentCount,
    int PlannedNewStudentCount,
    int PlannedTotalStudentCount,
    bool IsAvailableForAdvisorAssignment,
    int? AdvisorAssignmentCapacity
);

public record BulkAdvisorAssignmentPreviewItemDto(
    int StudentId,
    string StudentNumber,
    string StudentName,
    int ProgramId,
    string ProgramName,
    int DepartmentId,
    string DepartmentName,
    int CompletedSemesters,
    int? CurrentAdvisorId,
    string? CurrentAdvisorName,
    int SuggestedAdvisorId,
    string SuggestedAdvisorName,
    string Reason
);

public record BulkAdvisorAssignmentApplyResponseDto(
    int UpdatedCount,
    int SkippedCount,
    IReadOnlyList<BulkAdvisorAssignmentSkippedItemDto> SkippedItems
);

public record BulkAdvisorAssignmentSkippedItemDto(
    int StudentId,
    string Reason
);
public record BulkAdvisorSettingsUpdateRequestDto(
    List<BulkAdvisorSettingUpdateItemDto> Advisors
);

public record BulkAdvisorSettingUpdateItemDto(
    int AdvisorId,
    bool IsAvailableForAdvisorAssignment,
    int? AdvisorAssignmentCapacity
);

public record BulkAdvisorSettingsUpdateResponseDto(
    int UpdatedCount
);