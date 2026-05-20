namespace SIS.Application.Identity.DTOs;

// Categories
public record ReportCategoryDto(int Id, string Code, string Name, int ReportCount);
public record CreateReportCategoryDto(string Code, string Name);
public record UpdateReportCategoryDto(string Name); // Code immutable

// Reports
public record ReportDefinitionDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    int CategoryId,
    string CategoryCode,
    string CategoryName);

public record CreateReportDefinitionDto(string Code, string Name, string? Description, int CategoryId);
public record UpdateReportDefinitionDto(string Name, string? Description, int CategoryId);

// Category × Role matrix
public record CategoryRolePermissionDto(int CategoryId, int RoleId, string RoleName);

public record CategoryRoleMatrixRowDto(
    int CategoryId,
    string CategoryCode,
    string CategoryName,
    IReadOnlyList<int> AllowedRoleIds);

public record SetCategoryRolesDto(int CategoryId, IReadOnlyList<int> RoleIds);

// Per-report override
public record ReportPermissionOverrideDto(
    int Id,
    int ReportId,
    string ReportCode,
    string ReportName,
    int RoleId,
    string RoleName,
    bool IsAllowed,
    string? Reason,
    DateTime CreatedAt);

public record CreateReportOverrideDto(int ReportId, int RoleId, bool IsAllowed, string? Reason);

// Effective list (frontend için)
public record AccessibleReportDto(int Id, string Code, string Name, int CategoryId, string CategoryName);
