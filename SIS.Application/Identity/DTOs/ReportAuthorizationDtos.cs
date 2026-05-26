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

// ── Kullanıcı grupları ──
public record ReportUserGroupDto(int Id, string Name, string? Description, int MemberCount);
public record CreateReportUserGroupDto(string Name, string? Description);
public record UpdateReportUserGroupDto(string Name, string? Description);
public record ReportUserGroupMemberDto(int UserId, string FullName, string Email, DateTime AddedAt);
public record AddGroupMemberDto(int UserId);

// ── Birleşik yetki (grant) ──
// ScopeKind: 0=Category 1=Report | SubjectKind: 0=Role 1=User 2=Group | Effect: 0=Allow 1=Deny
public record ReportAccessGrantDto(
    int Id,
    int ScopeKind,
    int TargetId,
    string TargetLabel,     // kategori adı veya rapor adı
    int SubjectKind,
    int SubjectId,
    string SubjectLabel,    // rol adı / kullanıcı adı / grup adı
    int Effect,
    string? Reason,
    DateTime GrantedAt);

public record SetReportAccessGrantDto(
    int ScopeKind,
    int TargetId,
    int SubjectKind,
    int SubjectId,
    int Effect,
    string? Reason);

// Effective list (frontend için)
public record AccessibleReportDto(int Id, string Code, string Name, string? Description, int CategoryId, string CategoryName);
