using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/time-slots")]
[Authorize]
public sealed class TimeSlotController(ITimeSlotService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission("menu.ders-saatleri-yonetimi.view")]
    public async Task<IActionResult> GetByTerm([FromQuery] int termId, CancellationToken ct)
        => Ok(await service.GetByTermAsync(termId, ct));

    [HttpPost]
    [RequirePermission("menu.ders-saatleri-yonetimi.create")]
    public async Task<IActionResult> Create([FromBody] CreateTimeSlotDto dto, CancellationToken ct)
    {
        try { return Ok(await service.CreateAsync(dto, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.ders-saatleri-yonetimi.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTimeSlotDto dto, CancellationToken ct)
    {
        try { return Ok(await service.UpdateAsync(id, dto, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("menu.ders-saatleri-yonetimi.delete")]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool force = false, CancellationToken ct = default)
    {
        try
        {
            var result = await service.DeleteAsync(id, force, ct);
            if (!result.Deleted)
                return Conflict(result);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("regenerate")]
    [RequirePermission("menu.ders-saatleri-yonetimi.regenerate")]
    public async Task<IActionResult> Regenerate([FromQuery] int termId, [FromQuery] bool force = false, CancellationToken ct = default)
    {
        try
        {
            var result = await service.RegenerateForTermAsync(termId, force, ct);
            if (!result.Regenerated)
                return Conflict(result);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
