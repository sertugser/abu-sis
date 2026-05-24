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
    int PageSize = 25,
    IReadOnlyList<int>? DepartmentIds = null
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
