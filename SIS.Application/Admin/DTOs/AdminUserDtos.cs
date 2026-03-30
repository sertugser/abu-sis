namespace SIS.Application.Admin.DTOs;

public record AdminUserListItemDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    List<string> Roles,
    bool IsActive,
    string UserType  // "Student" | "Instructor" | "Admin" | "Other"
);

public record AdminUserDetailDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    List<string> Roles,
    bool IsActive,
    string UserType,
    string? StudentNumber,
    string? InstructorTitle,
    string? Department
);

public record AdminUserFilterDto(
    string? Search,
    string? Role,
    bool? IsActive,
    int Page = 1,
    int PageSize = 25
);

public record UpdateUserRolesDto(List<string> Roles);

public record AdminResetPasswordDto(string NewPassword);

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize
)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
