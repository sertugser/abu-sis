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

    // Category × Role matrix
    Task<IReadOnlyList<CategoryRoleMatrixRowDto>> GetCategoryRoleMatrixAsync(CancellationToken ct = default);
    Task SetCategoryRolesAsync(SetCategoryRolesDto dto, int grantedByUserId, CancellationToken ct = default);

    // Per-report override
    Task<IReadOnlyList<ReportPermissionOverrideDto>> ListOverridesAsync(int? reportId, CancellationToken ct = default);
    Task<ReportPermissionOverrideDto> UpsertOverrideAsync(CreateReportOverrideDto dto, int createdByUserId, CancellationToken ct = default);
    Task DeleteOverrideAsync(int id, CancellationToken ct = default);

    // Yetki çözümleme
    Task<bool> CanAccessReportAsync(int userId, string reportCode, CancellationToken ct = default);
    Task<IReadOnlyList<AccessibleReportDto>> ListAccessibleReportsAsync(int userId, CancellationToken ct = default);
}
