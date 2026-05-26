using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;

namespace SIS.API.Controllers.Admin;

[ApiController]
[Authorize]
[RequirePermission("system.permission-bundle.view")]
[Route("api/admin/permission-bundles")]
public class PermissionBundlesController : ControllerBase
{
    private readonly IPermissionBundleService _bundleService;
    private readonly RoleManager<IdentityRole<int>> _roleManager;

    private const string SystemAdminRole = "SystemAdministrator";

    public PermissionBundlesController(
        IPermissionBundleService bundleService,
        RoleManager<IdentityRole<int>> roleManager)
    {
        _bundleService = bundleService;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<ActionResult<List<PermissionBundleDto>>> GetAll(CancellationToken ct)
    {
        var bundles = await _bundleService.GetAllAsync(ct);
        return Ok(bundles);
    }

    [HttpGet("{bundleId:int}")]
    public async Task<ActionResult<PermissionBundleDetailDto>> GetById(int bundleId, CancellationToken ct)
    {
        var bundle = await _bundleService.GetByIdAsync(bundleId, ct);
        if (bundle is null) return NotFound();
        return Ok(bundle);
    }

    [HttpPost]
    [RequirePermission("system.permission-bundle.manage")]
    public async Task<ActionResult<PermissionBundleDetailDto>> Create(
        [FromBody] CreatePermissionBundleDto dto,
        CancellationToken ct)
    {
        if (dto is null) return BadRequest();
        try
        {
            var created = await _bundleService.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { bundleId = created.Id }, created);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{bundleId:int}")]
    [RequirePermission("system.permission-bundle.manage")]
    public async Task<ActionResult<PermissionBundleDetailDto>> Update(
        int bundleId,
        [FromBody] UpdatePermissionBundleDto dto,
        CancellationToken ct)
    {
        if (dto is null) return BadRequest();
        try
        {
            var updated = await _bundleService.UpdateAsync(bundleId, dto, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{bundleId:int}")]
    [RequirePermission("system.permission-bundle.manage")]
    public async Task<ActionResult> Delete(int bundleId, CancellationToken ct)
    {
        try
        {
            await _bundleService.DeleteAsync(bundleId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{bundleId:int}/apply/{roleId:int}")]
    [RequirePermission("system.permission-bundle.apply")]
    public async Task<ActionResult> ApplyToRole(int bundleId, int roleId, CancellationToken ct)
    {
        if (await IsSystemAdminRoleAsync(roleId))
        {
            return BadRequest(new { error = "SystemAdministrator rolü düzenlenemez (Karar 4 — locked)." });
        }

        var actorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(actorIdClaim, out var actorId)) return Unauthorized();

        try
        {
            await _bundleService.ApplyBundleToRoleAsync(roleId, bundleId, actorId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("{bundleId:int}/apply/{roleId:int}")]
    [RequirePermission("system.permission-bundle.apply")]
    public async Task<ActionResult> RemoveFromRole(int bundleId, int roleId, CancellationToken ct)
    {
        if (await IsSystemAdminRoleAsync(roleId))
        {
            return BadRequest(new { error = "SystemAdministrator rolü düzenlenemez (Karar 4 — locked)." });
        }
        await _bundleService.RemoveBundleFromRoleAsync(roleId, bundleId, ct);
        return NoContent();
    }

    [HttpGet("/api/admin/roles/{roleId:int}/applied-bundles")]
    public async Task<ActionResult<List<AppliedBundleDto>>> GetAppliedBundles(int roleId, CancellationToken ct)
    {
        var applied = await _bundleService.GetAppliedBundlesAsync(roleId, ct);
        return Ok(applied);
    }

    private async Task<bool> IsSystemAdminRoleAsync(int roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        return role?.Name == SystemAdminRole;
    }
}
