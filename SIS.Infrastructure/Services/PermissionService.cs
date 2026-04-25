using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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

        if (roles.Count == 0)
        {
            var emptySet = new HashSet<string>(StringComparer.Ordinal);
            _cache.Set(cacheKey, emptySet, CacheTtl);
            return emptySet;
        }

        // Normal user: rol-permission JOIN
        var roleIds = await _context.Roles
            .AsNoTracking()
            .Where(r => roles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(ct);

        var permissionKeys = await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission!.Key)
            .Distinct()
            .ToListAsync(ct);

        var resultSet = new HashSet<string>(permissionKeys, StringComparer.Ordinal);
        _cache.Set(cacheKey, resultSet, CacheTtl);
        return resultSet;
    }

    public async Task<bool> HasPermissionAsync(int userId, string permissionKey, CancellationToken ct = default)
    {
        var perms = await GetUserPermissionsAsync(userId, ct);
        return perms.Contains(permissionKey);
    }

    public async Task SetRolePermissionsAsync(
        int roleId,
        IReadOnlyList<int> permissionIds,
        int grantedByUserId,
        CancellationToken ct = default)
    {
        // Replace-all: önce mevcutları sil, sonra yenileri ekle (transaction içinde)
        var existing = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(ct);
        _context.RolePermissions.RemoveRange(existing);

        var rolePermissions = permissionIds
            .Distinct()
            .Select(pid => new RolePermission
            {
                RoleId = roleId,
                PermissionId = pid,
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
        return await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync(ct);
    }

    public void InvalidateUserCache(int userId)
    {
        _cache.Remove(CacheKeyForUser(userId));
    }

    public async Task InvalidateRoleCacheAsync(int roleId, CancellationToken ct = default)
    {
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
}
