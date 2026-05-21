using SIS.Application.Identity.DTOs;

namespace SIS.Application.Identity.Services;

/// <summary>
/// Admin Menu Editor servisi — sidebar yapısının (modüller, item'lar, item-permission bağları)
/// CRUD'u + reorder + cache invalidation. Frontend (TanStack Query) menü query'sini
/// mutation sonrası invalidate eder; backend'de menü cache'i yok (her çağrı DB).
/// </summary>
public interface IMenuAdminService
{
    Task<List<MenuModuleAdminDto>> GetTreeAsync(CancellationToken ct = default);

    Task<MenuModuleAdminDto> CreateModuleAsync(CreateMenuModuleDto dto, CancellationToken ct = default);
    Task<MenuModuleAdminDto> UpdateModuleAsync(int moduleId, UpdateMenuModuleDto dto, CancellationToken ct = default);
    Task DeleteModuleAsync(int moduleId, CancellationToken ct = default);
    Task ReorderModulesAsync(ReorderModulesDto dto, CancellationToken ct = default);

    Task<MenuItemAdminDto> CreateItemAsync(CreateMenuItemDto dto, CancellationToken ct = default);
    Task<MenuItemAdminDto> UpdateItemAsync(int itemId, UpdateMenuItemDto dto, CancellationToken ct = default);
    Task DeleteItemAsync(int itemId, CancellationToken ct = default);
    Task ReorderItemsAsync(ReorderItemsDto dto, CancellationToken ct = default);
}
