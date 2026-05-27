using SIS.Application.Identity.DTOs;

namespace SIS.Application.Identity.Services;

public interface IPermissionService
{
    /// <summary>
    /// Kullanıcının tüm permission key'lerini döner (rolleri üzerinden union).
    /// Cache'lidir (5 dk TTL, IMemoryCache).
    /// SystemAdmin rolü varsa tüm DB permission'ları döner (locked shortcut).
    /// </summary>
    Task<HashSet<string>> GetUserPermissionsAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Tek bir permission key'i kullanıcıda var mı? Cache hit yolu.
    /// </summary>
    Task<bool> HasPermissionAsync(int userId, string permissionKey, CancellationToken ct = default);

    /// <summary>
    /// Bir rolün MANUEL permission'larını TOPLU değiştirir (replace-all). Admin UI "Kaydet" butonu.
    /// Sadece SourceBundleId IS NULL satırları etkilenir; paketten gelen yetki satırlarına dokunulmaz
    /// (paket bağı korunur). Sonrasında ilgili rolün tüm kullanıcılarının cache'i invalidate edilir.
    /// </summary>
    Task SetRolePermissionsAsync(int roleId, IReadOnlyList<int> permissionIds, int grantedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Bir rolün mevcut permission ID listesini döner (manuel + paket, distinct, etkin set).
    /// </summary>
    Task<List<int>> GetRolePermissionIdsAsync(int roleId, CancellationToken ct = default);

    /// <summary>
    /// Bir rolün yetki durumunu manuel/paket ayrımıyla döner (admin matrisi için).
    /// </summary>
    Task<RolePermissionStateDto> GetRolePermissionStateAsync(int roleId, CancellationToken ct = default);

    /// <summary>
    /// Belirli bir rol adı için permission key'lerini döner (panel switcher için tek-rol filter).
    /// SystemAdmin shortcut UYGULANMAZ — sadece DB'deki RolePermissions'dan gelen explicit liste.
    /// Kullanıcının o role'e sahip olup olmadığı caller tarafından doğrulanmalı.
    /// </summary>
    Task<HashSet<string>> GetPermissionsByRoleNameAsync(string roleName, CancellationToken ct = default);

    /// <summary>
    /// Bir kullanıcının permission cache'ini invalidate eder (rol değişiminde).
    /// </summary>
    void InvalidateUserCache(int userId);

    /// <summary>
    /// Bir rolün TÜM kullanıcılarının permission cache'ini invalidate eder.
    /// Permission/role değişiminden sonra çağrılır.
    /// </summary>
    Task InvalidateRoleCacheAsync(int roleId, CancellationToken ct = default);
}
