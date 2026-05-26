using SIS.Application.Identity.DTOs;

namespace SIS.Application.Identity.Services;

public interface IReportAuthorizationService
{
    // Categories CRUD
    Task<IReadOnlyList<ReportCategoryDto>> ListCategoriesAsync(CancellationToken ct = default);
    Task<ReportCategoryDto> CreateCategoryAsync(CreateReportCategoryDto dto, CancellationToken ct = default);
    Task<ReportCategoryDto> UpdateCategoryAsync(int id, UpdateReportCategoryDto dto, CancellationToken ct = default);
    Task DeleteCategoryAsync(int id, CancellationToken ct = default);

    // Reports CRUD
    Task<IReadOnlyList<ReportDefinitionDto>> ListReportsAsync(int? categoryId, CancellationToken ct = default);
    Task<ReportDefinitionDto> CreateReportAsync(CreateReportDefinitionDto dto, CancellationToken ct = default);
    Task<ReportDefinitionDto> UpdateReportAsync(int id, UpdateReportDefinitionDto dto, CancellationToken ct = default);
    Task DeleteReportAsync(int id, CancellationToken ct = default);

    // Kullanıcı grupları
    Task<IReadOnlyList<ReportUserGroupDto>> ListGroupsAsync(CancellationToken ct = default);
    Task<ReportUserGroupDto> CreateGroupAsync(CreateReportUserGroupDto dto, CancellationToken ct = default);
    Task<ReportUserGroupDto> UpdateGroupAsync(int id, UpdateReportUserGroupDto dto, CancellationToken ct = default);
    Task DeleteGroupAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ReportUserGroupMemberDto>> ListGroupMembersAsync(int groupId, CancellationToken ct = default);
    Task AddGroupMemberAsync(int groupId, int userId, CancellationToken ct = default);
    Task RemoveGroupMemberAsync(int groupId, int userId, CancellationToken ct = default);

    // Birleşik yetki (grant)
    Task<IReadOnlyList<ReportAccessGrantDto>> ListGrantsAsync(CancellationToken ct = default);
    Task<ReportAccessGrantDto> SetGrantAsync(SetReportAccessGrantDto dto, int grantedByUserId, CancellationToken ct = default);
    Task DeleteGrantAsync(int id, CancellationToken ct = default);

    // Yetki çözümleme
    Task<bool> CanAccessReportAsync(int userId, string reportCode, CancellationToken ct = default);
    Task<IReadOnlyList<AccessibleReportDto>> ListAccessibleReportsAsync(int userId, CancellationToken ct = default);
}
