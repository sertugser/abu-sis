namespace SIS.Application.Admin.DTOs;

public record AdminInstructorListItemDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    string Title,
    string DepartmentName,
    string DepartmentCode,
    bool IsActive
);

public record AdminInstructorDetailDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    string Title,
    string DepartmentName,
    string DepartmentCode,
    int MaxWeeklyHours,
    bool IsActive,
    List<string> Roles
);

public record AdminInstructorFilterDto(
    string? Search,
    int? DepartmentId,
    string? Title,
    bool? IsActive
);

public record CreateInstructorDto(
    string FirstName,
    string LastName,
    string Title,
    int DepartmentId,
    int MaxWeeklyHours,
    string Password,
    string? OverrideEmail
);
