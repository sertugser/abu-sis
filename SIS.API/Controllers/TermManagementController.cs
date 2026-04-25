using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;

namespace SIS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/terms")]
public class TermManagementController : ControllerBase
{
    private readonly IAdminTermService _service;

    public TermManagementController(IAdminTermService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission("menu.akademik-donem.view")]
    public async Task<IActionResult> GetAllTerms()
        => Ok(await _service.GetAllTermsAsync());

    [HttpGet("{id:int}")]
    [RequirePermission("menu.akademik-donem.view")]
    public async Task<IActionResult> GetTerm(int id)
    {
        try { return Ok(await _service.GetTermByIdAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    [RequirePermission("menu.akademik-donem.create")]
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
    [RequirePermission("menu.akademik-donem.edit")]
    public async Task<IActionResult> UpdateTerm(int id, [FromBody] UpdateTermDto dto)
    {
        try { return Ok(await _service.UpdateTermAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/cancel")]
    [RequirePermission("menu.akademik-donem.cancel")]
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
    [RequirePermission("menu.akademik-donem.uncancel")]
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

    // Phase override moved to AcademicCalendarController (POST /api/admin/academic-calendar/{id}/override)
}
