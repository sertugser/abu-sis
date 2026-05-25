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
    string? AdvisorName,
    string? NationalId,
    bool IsScholarshipStudent,
    DateTime? EnrollmentDate,
    int? ClassYear
);

public record AdminStudentDetailDto(
    int Id,
    int UserId,
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
    int? AdvisorId,
    int? ProgramId,
    int? CurriculumId,
    string ProgramType,
    DateTime? EnrollmentDate,
    int? ClassYear,
    DateTime? DepartureDate,
    string? PreviousStudentNumber,
    string? NationalId,
    string? EntranceType,
    string? EntranceTypeLabel
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
    IReadOnlyList<int>? DepartmentIds = null,
    int? AdvisorId = null,
    bool? HasAdvisor = null,
    int? CurriculumId = null,
    int? ClassYear = null,
    string? StudentNoFrom = null,
    string? StudentNoTo = null,
    DateTime? EnrollmentFrom = null,
    DateTime? EnrollmentTo = null,
    bool? HasFailingCourse = null
);

public record UpdateStudentAcademicDto(
    string Status,
    int? AdvisorId,
    int ProgramId,
    int CurriculumId,
    string ProgramType,
    bool IsScholarshipStudent,
    bool HasFinancialBlock,
    DateTime? DepartureDate
);

public record ChangeStudentNumberDto(string NewStudentNumber);

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
