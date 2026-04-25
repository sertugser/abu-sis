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

    public MenuService(AppDbContext context, IPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
    }

    public async Task<List<MenuModuleDto>> GetUserMenuAsync(int userId, CancellationToken ct = default)
    {
        var permissions = await _permissionService.GetUserPermissionsAsync(userId, ct);

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
            .OrderBy(m => m.SortOrder)
            .Select(m => MapToDto(m, permissions, allModules))
            .Where(IsNonEmpty)
            .ToList();
    }

    private static MenuModuleDto MapToDto(
        MenuModule module,
        HashSet<string> permissions,
        List<MenuModule> allModules)
    {
        return new MenuModuleDto
        {
            Id = module.Id,
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
                .OrderBy(c => c.SortOrder)
                .Select(c => MapToDto(c, permissions, allModules))
                .Where(IsNonEmpty)
                .ToList(),
        };
    }

    /// <summary>
    /// Boş modül elimine: ne item'ı ne de visible alt-modülü olmayan modül kullanıcıya gösterilmez.
    /// </summary>
    private static bool IsNonEmpty(MenuModuleDto dto) => dto.Items.Count > 0 || dto.Children.Count > 0;
}
