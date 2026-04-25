using SIS.Application.Identity.DTOs;

namespace SIS.Application.Identity.Services;

public interface IMenuService
{
    /// <summary>
    /// Kullanıcı için menüyü döner — sadece permission'ı olduğu module/item'lar.
    /// Recursive tree (top-level + nested children).
    /// Boş modül (item'sız ve children'sız) silent eleminate edilir.
    ///
    /// Patch 3 (panel switcher): <paramref name="activeRole"/> verilirse, kullanıcının
    /// TÜM rollerinin union'u yerine SADECE o role'in DB permission'larıyla filter yapılır.
    /// SystemAdmin shortcut bu durumda BYPASS edilir (SysAdmin Dean paneline girince
    /// Dean'in modüllerini görür). Ayrıca AllowedRolesCsv kontrolü için kullanıcının
    /// rol listesinde activeRole bulunmalı (caller doğrular).
    /// </summary>
    Task<List<MenuModuleDto>> GetUserMenuAsync(int userId, string? activeRole = null, CancellationToken ct = default);
}
