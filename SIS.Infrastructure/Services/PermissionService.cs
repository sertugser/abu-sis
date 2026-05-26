using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly UserManager<ApplicationUser> _userManager;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const string SystemAdminRole = "SystemAdministrator";

    public PermissionService(
        AppDbContext context,
        IMemoryCache cache,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _cache = cache;
        _userManager = userManager;
    }

    public async Task<HashSet<string>> GetUserPermissionsAsync(int userId, CancellationToken ct = default)
    {
        var cacheKey = CacheKeyForUser(userId);
        if (_cache.TryGetValue<HashSet<string>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var roles = await _userManager.GetRolesAsync(user);

        // SystemAdmin shortcut: tüm DB permission'larını döner (Karar 4 — locked)
        if (roles.Contains(SystemAdminRole))
        {
            var allKeys = await _context.Permissions
                .AsNoTracking()
                .Select(p => p.Key)
                .ToListAsync(ct);
            var allSet = new HashSet<string>(allKeys, StringComparer.Ordinal);
            _cache.Set(cacheKey, allSet, CacheTtl);
            return allSet;
        }

        // Aktif vekâletlerden gelen delegator user'larını topla (kendi rolleri ∪ devralınan roller).
        // Yalnızca AKTİF (kilitli olmayan) delegator'lar sayılır — kilitli/pasif hesabın yetkileri akmaz (#10).
        var now = DateTime.UtcNow;
        var nowOffset = DateTimeOffset.UtcNow;
        var delegatorIds = await (
            from d in _context.PermissionDelegations.AsNoTracking()
            where d.DelegateeUserId == userId
                  && d.RevokedAtUtc == null
                  && d.StartsAtUtc <= now
                  && now <= d.EndsAtUtc
            join u in _context.Users.AsNoTracking() on d.DelegatorUserId equals u.Id
            where !u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= nowOffset
            select d.DelegatorUserId)
            .Distinct()
            .ToListAsync(ct);

        // Bir sonraki vekâlet sınırı (aktif olanın bitişi / pending olanın başlangıcı) cache TTL'ini
        // kısıtlar — böylece doğal süre bitiminde/başlangıcında yetki seti tam zamanında tazelenir,
        // 5 dk'lık stale pencere oluşmaz (#6). Pending varsa delegatorIds boş olsa bile kısıtlanır.
        var nextBoundary = await _context.PermissionDelegations
            .AsNoTracking()
            .Where(d => d.DelegateeUserId == userId && d.RevokedAtUtc == null && d.EndsAtUtc > now)
            .Select(d => (DateTime?)(d.StartsAtUtc > now ? d.StartsAtUtc : d.EndsAtUtc))
            .OrderBy(t => t)
            .FirstOrDefaultAsync(ct);

        var cacheTtl = CacheTtl;
        if (nextBoundary is { } boundary)
        {
            var untilBoundary = boundary - now;
            if (untilBoundary > TimeSpan.Zero && untilBoundary < cacheTtl)
            {
                cacheTtl = untilBoundary;
            }
        }

        // Normal user: kendi rolleri + delegator rolleri toplam set.
        // SystemAdministrator vekâletle DEVREDİLEMEZ (Create'te de bloke) — burada da dışlanır ki
        // eski/legacy kayıtlar bile tam-yetki yükseltmesine yol açmasın (güvenlik, tutarlılık).
        var combinedRoleNames = new HashSet<string>(roles, StringComparer.Ordinal);
        if (delegatorIds.Count > 0)
        {
            var extraRoleNames = await (
                from ur in _context.Set<IdentityUserRole<int>>().AsNoTracking()
                join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
                where delegatorIds.Contains(ur.UserId) && r.Name != SystemAdminRole
                select r.Name!
            ).Distinct().ToListAsync(ct);
            foreach (var n in extraRoleNames) combinedRoleNames.Add(n);
        }

        if (combinedRoleNames.Count == 0)
        {
            var emptySet = new HashSet<string>(StringComparer.Ordinal);
            _cache.Set(cacheKey, emptySet, cacheTtl);
            return emptySet;
        }

        var roleIds = await _context.Roles
            .AsNoTracking()
            .Where(r => combinedRoleNames.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(ct);

        var permissionKeys = await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission!.Key)
            .Distinct()
            .ToListAsync(ct);

        var resultSet = new HashSet<string>(permissionKeys, StringComparer.Ordinal);
        _cache.Set(cacheKey, resultSet, cacheTtl);
        return resultSet;
    }

    public async Task<bool> HasPermissionAsync(int userId, string permissionKey, CancellationToken ct = default)
    {
        var perms = await GetUserPermissionsAsync(userId, ct);
        return perms.Contains(permissionKey);
    }

    public async Task<HashSet<string>> GetPermissionsByRoleNameAsync(string roleName, CancellationToken ct = default)
    {
        // Patch 3: Panel switcher için — sadece o role'in DB'deki permission'ları döner.
        // Multi-rol kullanıcı (örn. SysAdmin + Dean) "Dekan paneli"ne geçerse Dean'in
        // explicit DB permission'larını görür — kendi SysAdmin "her şey" yetkisini taşımaz.
        var cacheKey = CacheKeyForRole(roleName);
        if (_cache.TryGetValue<HashSet<string>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        // SystemAdministrator role'ünün KENDISI = tüm DB permission'ları (kod-shortcut, Karar 4).
        // DB'de RolePermission satırı yoktur; sorulan rol SystemAdmin ise Permissions tablosunun tamamı döner.
        // Bu sayede SysAdmin "SystemAdmin paneli"ndeyken bomboş menü görmez.
        if (roleName == SystemAdminRole)
        {
            var allKeys = await _context.Permissions
                .AsNoTracking()
                .Select(p => p.Key)
                .ToListAsync(ct);
            var allSet = new HashSet<string>(allKeys, StringComparer.Ordinal);
            _cache.Set(cacheKey, allSet, CacheTtl);
            return allSet;
        }

        var role = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == roleName, ct);
        if (role is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var keys = await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.Permission!.Key)
            .Distinct()
            .ToListAsync(ct);

        var set = new HashSet<string>(keys, StringComparer.Ordinal);
        _cache.Set(cacheKey, set, CacheTtl);
        return set;
    }

    public async Task SetRolePermissionsAsync(
        int roleId,
        IReadOnlyList<int> permissionIds,
        int grantedByUserId,
        CancellationToken ct = default)
    {
        // Replace-all SADECE manuel satırlarda: paketten gelen (SourceBundleId IS NOT NULL)
        // satırlara dokunulmaz; aksi halde matris "Kaydet" paket bağını sessizce silerdi.
        var existingManual = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId && rp.SourceBundleId == null)
            .ToListAsync(ct);
        _context.RolePermissions.RemoveRange(existingManual);

        var rolePermissions = permissionIds
            .Distinct()
            .Select(pid => new RolePermission
            {
                RoleId = roleId,
                PermissionId = pid,
                SourceBundleId = null,
                GrantedAt = DateTime.UtcNow,
                GrantedByUserId = grantedByUserId,
            })
            .ToList();
        await _context.RolePermissions.AddRangeAsync(rolePermissions, ct);
        await _context.SaveChangesAsync(ct);

        // Bu rolü taşıyan tüm kullanıcıların cache'i invalid
        await InvalidateRoleCacheAsync(roleId, ct);
    }

    public async Task<List<int>> GetRolePermissionIdsAsync(int roleId, CancellationToken ct = default)
    {
        // Etkin set: aynı permission hem manuel hem paketten gelirse tek görünür (Distinct).
        return await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<RolePermissionStateDto> GetRolePermissionStateAsync(int roleId, CancellationToken ct = default)
    {
        var rows = await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => new { rp.PermissionId, rp.SourceBundleId })
            .ToListAsync(ct);

        return new RolePermissionStateDto
        {
            ManualPermissionIds = rows
                .Where(r => r.SourceBundleId == null)
                .Select(r => r.PermissionId)
                .Distinct()
                .ToList(),
            BundlePermissionIds = rows
                .Where(r => r.SourceBundleId != null)
                .Select(r => r.PermissionId)
                .Distinct()
                .ToList(),
        };
    }

    public void InvalidateUserCache(int userId)
    {
        _cache.Remove(CacheKeyForUser(userId));
    }

    public async Task InvalidateRoleCacheAsync(int roleId, CancellationToken ct = default)
    {
        // Önce role-name'i bul → role-cache key'i de invalidate et (Patch 3)
        var roleName = await _context.Roles
            .AsNoTracking()
            .Where(r => r.Id == roleId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync(ct);
        if (roleName is not null)
        {
            _cache.Remove(CacheKeyForRole(roleName));
        }

        // IdentityDbContext UserRoles'u expose etmez doğrudan; AspNetUserRoles tablosuna direkt sorgu
        var userIds = await _context.Set<IdentityUserRole<int>>()
            .AsNoTracking()
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .ToListAsync(ct);

        foreach (var userId in userIds)
        {
            InvalidateUserCache(userId);
        }
    }

    private static string CacheKeyForUser(int userId) => $"permissions:user:{userId}";
    private static string CacheKeyForRole(string roleName) => $"permissions:role:{roleName}";
}
