using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/osym-registration-calendars")]
[Authorize]
public class OsymRegistrationCalendarController(IOsymRegistrationCalendarService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission("menu.osym-kayit-takvimi.view")]
    public async Task<IActionResult> GetAll([FromQuery] int? year, CancellationToken ct)
        => Ok(await service.GetAllAsync(year, ct));

    [HttpPost]
    [RequirePermission("menu.osym-kayit-takvimi.create")]
    public async Task<IActionResult> Create([FromBody] CreateOsymRegistrationCalendarDto dto, CancellationToken ct)
    {
        try { return Ok(await service.CreateAsync(dto, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.osym-kayit-takvimi.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOsymRegistrationCalendarDto dto, CancellationToken ct)
    {
        try { return Ok(await service.UpdateAsync(id, dto, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("menu.osym-kayit-takvimi.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try { await service.DeleteAsync(id, ct); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}
