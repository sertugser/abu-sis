using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly IPermissionService _permissionService;
    private readonly IMenuService _menuService;
    private readonly IPermissionDelegationService _delegationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public MeController(
        IPermissionService permissionService,
        IMenuService menuService,
        IPermissionDelegationService delegationService,
        UserManager<ApplicationUser> userManager)
    {
        _permissionService = permissionService;
        _menuService = menuService;
        _delegationService = delegationService;
        _userManager = userManager;
    }

    /// <summary>
    /// Kullanıcının tüm permission key'lerini döner.
    /// Frontend PermissionContext bunu 5 dk cache'ler (Karar 6 — JWT'de claim yok).
    /// </summary>
    [HttpGet("permissions")]
    [RequirePermission("lookup.me")]
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
    [RequirePermission("lookup.me")]
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

    /// <summary>
    /// Kullanıcının kendi rolleri + aktif vekâletten devraldığı rolleri döner.
    /// Frontend panel switcher availableRoles'u bu endpoint'ten kurar.
    /// </summary>
    [HttpGet("effective-roles")]
    public async Task<ActionResult<EffectiveRolesDto>> GetEffectiveRoles(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        var ownRoles = user is null
            ? Array.Empty<string>()
            : (await _userManager.GetRolesAsync(user)).ToArray();

        var delegated = await _delegationService.GetActiveDelegatedRoleNamesAsync(userId, ct);

        // Vekâletten gelen ama kullanıcının zaten kendi sahip olduğu rolleri dışla (duplicate olmasın)
        var ownSet = new HashSet<string>(ownRoles, StringComparer.Ordinal);
        var delegatedOnly = delegated.Where(r => !ownSet.Contains(r)).ToArray();

        return Ok(new EffectiveRolesDto(ownRoles, delegatedOnly));
    }

    private bool TryGetUserId(out int userId)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out userId);
    }
}

public record EffectiveRolesDto(IReadOnlyList<string> OwnRoles, IReadOnlyList<string> DelegatedRoles);
