using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;

namespace SIS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly IPermissionService _permissionService;
    private readonly IMenuService _menuService;

    public MeController(IPermissionService permissionService, IMenuService menuService)
    {
        _permissionService = permissionService;
        _menuService = menuService;
    }

    /// <summary>
    /// Kullanıcının tüm permission key'lerini döner.
    /// Frontend PermissionContext bunu 5 dk cache'ler (Karar 6 — JWT'de claim yok).
    /// </summary>
    [HttpGet("permissions")]
    public async Task<ActionResult<List<string>>> GetMyPermissions(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var permissions = await _permissionService.GetUserPermissionsAsync(userId, ct);
        return Ok(permissions.OrderBy(k => k).ToList());
    }

    /// <summary>
    /// Kullanıcının dinamik menüsünü döner — sidebar render'ı için.
    /// SidebarModules.ts hardcoded array'lerinin yerini alır.
    ///
    /// Patch 3 (panel switcher): <c>?role=Dean</c> query param ile multi-rol kullanıcı
    /// belirli bir panele filtrelenebilir. MenuService kullanıcının o role'e sahip olup
    /// olmadığını doğrular; yoksa union davranışına düşer.
    /// </summary>
    [HttpGet("menu")]
    public async Task<ActionResult<List<MenuModuleDto>>> GetMyMenu(
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var menu = await _menuService.GetUserMenuAsync(userId, role, ct);
        return Ok(menu);
    }

    private bool TryGetUserId(out int userId)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out userId);
    }
}
