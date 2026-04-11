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
    {
        var result = await _service.GetAllTermsAsync();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTerm(int id)
    {
        try
        {
            var result = await _service.GetTermByIdAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateTerm([FromBody] CreateTermDto dto)
    {
        try
        {
            var result = await _service.CreateTermAsync(dto);
            return CreatedAtAction(nameof(GetTerm), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTerm(int id, [FromBody] UpdateTermDto dto)
    {
        try
        {
            var result = await _service.UpdateTermAsync(id, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> ActivateTerm(int id)
    {
        try
        {
            await _service.ActivateTermAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Dönem durumunu bir sonraki aşamaya geçirir.
    /// Draft → Planning → Scheduling → Published → RegistrationOpen →
    /// AddDrop → WithdrawalPeriod → GradeEntry → Closed → Archived
    /// </summary>
    [HttpPost("{id:int}/transition")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> TransitionStatus(int id, [FromBody] TransitionTermStatusDto dto)
    {
        try
        {
            await _service.TransitionStatusAsync(id, dto.TargetStatus);
            var updated = await _service.GetTermByIdAsync(id);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Calendar Events ──────────────────────────────────────────────────────

    [HttpGet("{id:int}/calendar-events")]
    public async Task<IActionResult> GetCalendarEvents(int id)
    {
        try
        {
            var result = await _service.GetCalendarEventsAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/calendar-events")]
    public async Task<IActionResult> CreateCalendarEvent(int id, [FromBody] CreateCalendarEventDto dto)
    {
        try
        {
            var result = await _service.CreateCalendarEventAsync(id, dto);
            return CreatedAtAction(nameof(GetCalendarEvents), new { id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/calendar-events/{eventId:int}")]
    public async Task<IActionResult> UpdateCalendarEvent(int id, int eventId, [FromBody] UpdateCalendarEventDto dto)
    {
        try
        {
            var result = await _service.UpdateCalendarEventAsync(id, eventId, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}/calendar-events/{eventId:int}")]
    public async Task<IActionResult> DeleteCalendarEvent(int id, int eventId)
    {
        try
        {
            await _service.DeleteCalendarEventAsync(id, eventId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
