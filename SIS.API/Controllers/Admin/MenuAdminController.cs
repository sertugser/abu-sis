using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers.Admin;

[ApiController]
[Authorize]
[RequirePermission("system.menu.view")]
[Route("api/admin/menu")]
public class MenuAdminController : ControllerBase
{
    private readonly IMenuAdminService _service;

    public MenuAdminController(IMenuAdminService service)
    {
        _service = service;
    }

    [HttpGet("tree")]
    public async Task<ActionResult<List<MenuModuleAdminDto>>> GetTree(CancellationToken ct)
    {
        return Ok(await _service.GetTreeAsync(ct));
    }

    [HttpPost("modules")]
    [RequirePermission("system.menu.manage")]
    public async Task<ActionResult<MenuModuleAdminDto>> CreateModule([FromBody] CreateMenuModuleDto dto, CancellationToken ct)
    {
        if (dto is null) return BadRequest();
        try { return Ok(await _service.CreateModuleAsync(dto, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("modules/{moduleId:int}")]
    [RequirePermission("system.menu.manage")]
    public async Task<ActionResult<MenuModuleAdminDto>> UpdateModule(int moduleId, [FromBody] UpdateMenuModuleDto dto, CancellationToken ct)
    {
        if (dto is null) return BadRequest();
        try { return Ok(await _service.UpdateModuleAsync(moduleId, dto, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("modules/{moduleId:int}")]
    [RequirePermission("system.menu.manage")]
    public async Task<ActionResult> DeleteModule(int moduleId, CancellationToken ct)
    {
        try
        {
            await _service.DeleteModuleAsync(moduleId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("modules/reorder")]
    [RequirePermission("system.menu.manage")]
    public async Task<ActionResult> ReorderModules([FromBody] ReorderModulesDto dto, CancellationToken ct)
    {
        if (dto is null) return BadRequest();
        try
        {
            await _service.ReorderModulesAsync(dto, ct);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("items")]
    [RequirePermission("system.menu.manage")]
    public async Task<ActionResult<MenuItemAdminDto>> CreateItem([FromBody] CreateMenuItemDto dto, CancellationToken ct)
    {
        if (dto is null) return BadRequest();
        try { return Ok(await _service.CreateItemAsync(dto, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("items/{itemId:int}")]
    [RequirePermission("system.menu.manage")]
    public async Task<ActionResult<MenuItemAdminDto>> UpdateItem(int itemId, [FromBody] UpdateMenuItemDto dto, CancellationToken ct)
    {
        if (dto is null) return BadRequest();
        try { return Ok(await _service.UpdateItemAsync(itemId, dto, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("items/{itemId:int}")]
    [RequirePermission("system.menu.manage")]
    public async Task<ActionResult> DeleteItem(int itemId, CancellationToken ct)
    {
        try
        {
            await _service.DeleteItemAsync(itemId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("items/reorder")]
    [RequirePermission("system.menu.manage")]
    public async Task<ActionResult> ReorderItems([FromBody] ReorderItemsDto dto, CancellationToken ct)
    {
        if (dto is null) return BadRequest();
        try
        {
            await _service.ReorderItemsAsync(dto, ct);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Editor formunun "permission seçici" dropdown'u için flat liste.
    /// </summary>
    [HttpGet("available-permissions")]
    public async Task<ActionResult<List<PermissionDto>>> GetAvailablePermissions(
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var perms = await db.Permissions
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
}
