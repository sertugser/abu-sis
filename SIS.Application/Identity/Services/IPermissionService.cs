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
    /// Bir rolün permission'larını TOPLU değiştirir (replace-all). Admin UI "Kaydet" butonu.
    /// Sonrasında ilgili rolün tüm kullanıcılarının cache'i invalidate edilir.
    /// </summary>
    Task SetRolePermissionsAsync(int roleId, IReadOnlyList<int> permissionIds, int grantedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Bir rolün mevcut permission ID listesini döner (admin UI matrisi için).
    /// </summary>
    Task<List<int>> GetRolePermissionIdsAsync(int roleId, CancellationToken ct = default);

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
