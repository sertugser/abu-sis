using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers.Admin;

[ApiController]
[Authorize]
[RequirePermission("system.permission.view")]
[Route("api/admin/permissions")]
public class PermissionAdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPermissionService _permissionService;
    private readonly RoleManager<IdentityRole<int>> _roleManager;

    private const string SystemAdminRole = "SystemAdministrator";

    public PermissionAdminController(
        AppDbContext context,
        IPermissionService permissionService,
        RoleManager<IdentityRole<int>> roleManager)
    {
        _context = context;
        _permissionService = permissionService;
        _roleManager = roleManager;
    }

    /// <summary>
    /// Tüm DB-seed permission'ları döner (admin matrisi için).
    /// lookup.* DB'de yok (hardcoded shortcut), bu yüzden listede de yok.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<PermissionDto>>> GetAllPermissions(CancellationToken ct)
    {
        var perms = await _context.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Key)
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Key = p.Key,
                Category = p.Category,
                Description = p.Description,
            })
            .ToListAsync(ct);
        return Ok(perms);
    }

    /// <summary>
    /// Atanabilir rolleri döner (SystemAdministrator hariç — Karar 4 locked).
    /// </summary>
    [HttpGet("roles")]
    public ActionResult<List<RoleDto>> GetAssignableRoles()
    {
        var roles = _roleManager.Roles
            .Where(r => r.Name != SystemAdminRole)
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto { Id = r.Id, Name = r.Name ?? string.Empty })
            .ToList();
        return Ok(roles);
    }

    /// <summary>
    /// Bir rolün yetki durumunu manuel/paket ayrımıyla döner (matris checkbox state).
    /// Manuel olanlar düzenlenebilir; paketten gelenler kilitli gösterilir.
    /// </summary>
    [HttpGet("roles/{roleId:int}")]
    public async Task<ActionResult<RolePermissionStateDto>> GetRolePermissions(int roleId, CancellationToken ct)
    {
        if (await IsSystemAdminRoleAsync(roleId))
        {
            return BadRequest(new { error = "SystemAdministrator rolü düzenlenemez (Karar 4 — locked)." });
        }

        var state = await _permissionService.GetRolePermissionStateAsync(roleId, ct);
        return Ok(state);
    }

    /// <summary>
    /// Bir rolün permission'larını TOPLU değiştirir (replace-all).
    /// Save sonrası ilgili rolün tüm kullanıcılarının cache'i invalidate edilir.
    /// </summary>
    [HttpPut("roles/{roleId:int}")]
    [RequirePermission("system.permission.assign")]
    public async Task<ActionResult> UpdateRolePermissions(
        int roleId,
        [FromBody] UpdateRolePermissionsDto dto,
        CancellationToken ct)
    {
        if (dto is null)
        {
            return BadRequest();
        }

        if (await IsSystemAdminRoleAsync(roleId))
        {
            return BadRequest(new { error = "SystemAdministrator rolü düzenlenemez (Karar 4 — locked)." });
        }

        var actorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(actorIdClaim, out var actorId))
        {
            return Unauthorized();
        }

        await _permissionService.SetRolePermissionsAsync(roleId, dto.PermissionIds, actorId, ct);
        return NoContent();
    }

    private async Task<bool> IsSystemAdminRoleAsync(int roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        return role?.Name == SystemAdminRole;
    }
}

public class RoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
