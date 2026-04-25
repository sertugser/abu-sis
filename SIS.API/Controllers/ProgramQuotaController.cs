using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/program-quotas")]
[Authorize]
public class ProgramQuotaController(IProgramQuotaService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission("menu.program-kontenjan.view")]
    public async Task<IActionResult> GetAll([FromQuery] int? programId, [FromQuery] int? year, CancellationToken ct)
        => Ok(await service.GetAllAsync(programId, year, ct));

    [HttpPost]
    [RequirePermission("menu.program-kontenjan.create")]
    public async Task<IActionResult> Create([FromBody] CreateProgramQuotaDto dto, CancellationToken ct)
    {
        try { return Ok(await service.CreateAsync(dto, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.program-kontenjan.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProgramQuotaDto dto, CancellationToken ct)
    {
        try { return Ok(await service.UpdateAsync(id, dto, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("menu.program-kontenjan.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try { await service.DeleteAsync(id, ct); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}
