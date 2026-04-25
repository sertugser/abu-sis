using SIS.Application.Identity.DTOs;

namespace SIS.Application.Identity.Services;

public interface IMenuService
{
    /// <summary>
    /// Kullanıcı için menüyü döner — sadece permission'ı olduğu module/item'lar.
    /// Recursive tree (top-level + nested children).
    /// Boş modül (item'sız ve children'sız) silent eleminate edilir.
    /// </summary>
    Task<List<MenuModuleDto>> GetUserMenuAsync(int userId, CancellationToken ct = default);
}
