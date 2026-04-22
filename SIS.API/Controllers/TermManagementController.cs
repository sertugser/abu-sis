using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/terms")]
[Authorize(Roles = "SystemAdministrator,AdministrativeStaff")]
public class TermManagementController : ControllerBase
{
    private readonly IAdminTermService _service;

    public TermManagementController(IAdminTermService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllTerms()
        => Ok(await _service.GetAllTermsAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTerm(int id)
    {
        try { return Ok(await _service.GetTermByIdAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> CreateTerm([FromBody] CreateTermDto dto)
    {
        try
        {
            var result = await _service.CreateTermAsync(dto);
            return CreatedAtAction(nameof(GetTerm), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTerm(int id, [FromBody] UpdateTermDto dto)
    {
        try { return Ok(await _service.UpdateTermAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            await _service.CancelTermAsync(id);
            return Ok(await _service.GetTermByIdAsync(id));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/uncancel")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> Uncancel(int id)
    {
        try
        {
            await _service.UncancelTermAsync(id);
            return Ok(await _service.GetTermByIdAsync(id));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/override")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> UpdatePhaseOverride(int id, [FromBody] UpdateTermPhaseOverrideDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int? userId = int.TryParse(userIdClaim, out var uid) ? uid : null;

            await _service.UpdatePhaseOverrideAsync(id, dto, userId);
            return Ok(await _service.GetTermByIdAsync(id));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
