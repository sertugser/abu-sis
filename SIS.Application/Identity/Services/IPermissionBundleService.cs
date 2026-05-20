using SIS.Application.Identity.DTOs;

namespace SIS.Application.Identity.Services;

public interface IPermissionBundleService
{
    Task<List<PermissionBundleDto>> GetAllAsync(CancellationToken ct = default);
    Task<PermissionBundleDetailDto?> GetByIdAsync(int bundleId, CancellationToken ct = default);
    Task<PermissionBundleDetailDto> CreateAsync(CreatePermissionBundleDto dto, CancellationToken ct = default);
    Task<PermissionBundleDetailDto> UpdateAsync(int bundleId, UpdatePermissionBundleDto dto, CancellationToken ct = default);
    Task DeleteAsync(int bundleId, CancellationToken ct = default);

    /// <summary>
    /// Bir bundle'ın tüm permission'larını role'e uygular (additive — Karar 2 union semantics).
    /// Aynı bundle'dan gelen eski satırlar silinir, yeni snapshot eklenir.
    /// Manuel atanmış permission'lar korunur.
    /// </summary>
    Task ApplyBundleToRoleAsync(int roleId, int bundleId, int actorUserId, CancellationToken ct = default);

    /// <summary>
    /// Bir bundle'ın role'den çıkarılması — sadece bu bundle'dan gelen RolePermission satırları silinir.
    /// </summary>
    Task RemoveBundleFromRoleAsync(int roleId, int bundleId, CancellationToken ct = default);

    /// <summary>
    /// Bir role'e uygulanmış bundle'ların ID listesi.
    /// </summary>
    Task<List<int>> GetAppliedBundleIdsAsync(int roleId, CancellationToken ct = default);
}
