using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class MenuService : IMenuService
{
    private readonly AppDbContext _context;
    private readonly IPermissionService _permissionService;
    private readonly UserManager<ApplicationUser> _userManager;

    public MenuService(
        AppDbContext context,
        IPermissionService permissionService,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _permissionService = permissionService;
        _userManager = userManager;
    }

    public async Task<List<MenuModuleDto>> GetUserMenuAsync(int userId, CancellationToken ct = default)
    {
        var permissions = await _permissionService.GetUserPermissionsAsync(userId, ct);

        // Patch 2C: Modül-bazlı role filter için user'ın role'lerini al.
        // SystemAdmin shortcut bu kontrolü BYPASS ETMEZ — modül AllowedRolesCsv kuralı
        // her kullanıcı için (admin dahil) çalışır.
        var user = await _userManager.FindByIdAsync(userId.ToString());
        var userRoles = user is not null
            ? (await _userManager.GetRolesAsync(user)).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        // v2 perf: AsSplitQuery (cartesian explosion önler) + AsNoTracking (read-only).
        // EF Core .Include(Children) sadece 1 seviye yükler — biz tüm modülleri tek query
        // ile çekip parent-child ilişkisini in-memory'de kuruyoruz (recursive support).
        var allModules = await _context.MenuModules
            .AsNoTracking()
            .AsSplitQuery()
            .Where(m => m.IsActive)
            .Include(m => m.Items.Where(i => i.IsActive))
                .ThenInclude(i => i.MenuItemPermissions)
                    .ThenInclude(mip => mip.Permission)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);

        // Top-level (ParentId == null) modüllerden başla, recursive map et
        return allModules
            .Where(m => m.ParentId == null)
            .Where(m => IsModuleAllowedForRoles(m, userRoles))
            .OrderBy(m => m.SortOrder)
            .Select(m => MapToDto(m, permissions, allModules, userRoles))
            .Where(IsNonEmpty)
            .ToList();
    }

    /// <summary>
    /// Modülün AllowedRolesCsv kuralına göre kullanıcı için gözükebilir mi?
    /// - Null/boş → herkes görür (default davranış)
    /// - CSV → kullanıcının role'lerinden EN AZ BIRI listede olmalı
    /// </summary>
    private static bool IsModuleAllowedForRoles(MenuModule module, HashSet<string> userRoles)
    {
        if (string.IsNullOrWhiteSpace(module.AllowedRolesCsv)) return true;
        var allowed = module.AllowedRolesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return allowed.Any(r => userRoles.Contains(r));
    }

    private static MenuModuleDto MapToDto(
        MenuModule module,
        HashSet<string> permissions,
        List<MenuModule> allModules,
        HashSet<string> userRoles)
    {
        return new MenuModuleDto
        {
            Id = module.Id,
            Slug = module.Slug,
            Label = module.Label,
            Icon = module.Icon,
            SortOrder = module.SortOrder,
            Items = module.Items
                // Item'a bağlı permission'lardan EN AZ BIRI user'da varsa (OR mantığı)
                .Where(i => i.MenuItemPermissions.Any(mip =>
                    mip.Permission != null && permissions.Contains(mip.Permission.Key)))
                .OrderBy(i => i.SortOrder)
                .Select(i => new MenuItemDto
                {
                    Id = i.Id,
                    Label = i.Label,
                    Route = i.Route,
                    Slug = i.Slug,
                    SortOrder = i.SortOrder,
                    OpenInNewTab = i.OpenInNewTab,
                })
                .ToList(),
            Children = allModules
                .Where(c => c.ParentId == module.Id)
                .Where(c => IsModuleAllowedForRoles(c, userRoles))
                .OrderBy(c => c.SortOrder)
                .Select(c => MapToDto(c, permissions, allModules, userRoles))
                .Where(IsNonEmpty)
                .ToList(),
        };
    }

    /// <summary>
    /// Boş modül elimine: ne item'ı ne de visible alt-modülü olmayan modül kullanıcıya gösterilmez.
    /// </summary>
    private static bool IsNonEmpty(MenuModuleDto dto) => dto.Items.Count > 0 || dto.Children.Count > 0;
}
