using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.AcademicCalendar.DTOs;
using SIS.Application.AcademicCalendar.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api")]
public class AcademicCalendarController : ControllerBase
{
    private readonly IAcademicCalendarService _svc;

    public AcademicCalendarController(IAcademicCalendarService svc) { _svc = svc; }

    // Admin

    [HttpGet("admin/academic-calendar")]
    [Authorize(Roles = "SystemAdministrator,AdministrativeStaff")]
    public async Task<IActionResult> ListTerms(CancellationToken ct)
        => Ok(await _svc.ListTermsAsync(ct));

    [HttpGet("admin/academic-calendar/{termId:int}")]
    [Authorize(Roles = "SystemAdministrator,AdministrativeStaff")]
    public async Task<IActionResult> GetByTerm(int termId, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.GetByTermIdAsync(termId, ct);
            return dto is null ? NotFound(new { message = "Akademik takvim tanımlı değil." }) : Ok(dto);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPut("admin/academic-calendar/{termId:int}")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> Upsert(int termId, [FromBody] UpsertAcademicCalendarDto dto, CancellationToken ct)
    {
        try { return Ok(await _svc.UpsertAsync(termId, dto, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("admin/academic-calendar/{termId:int}/override")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> UpdatePhaseOverride(int termId, [FromBody] UpdateCalendarPhaseOverrideDto dto, CancellationToken ct)
    {
        try
        {
            await _svc.UpdatePhaseOverrideAsync(termId, dto, ct);
            var fresh = await _svc.GetByTermIdAsync(termId, ct);
            return Ok(fresh);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // Public (authenticated)

    [HttpGet("academic-calendar/active")]
    [Authorize]
    public async Task<IActionResult> GetActiveStudent(CancellationToken ct)
    {
        var view = await _svc.GetActiveStudentViewAsync(ct);
        return view is null ? NotFound(new { message = "Aktif dönem bulunamadı." }) : Ok(view);
    }

    [HttpGet("academic-calendar/{termId:int}")]
    [Authorize]
    public async Task<IActionResult> GetStudent(int termId, CancellationToken ct)
    {
        var view = await _svc.GetStudentViewAsync(termId, ct);
        return view is null ? NotFound(new { message = "Dönem bulunamadı." }) : Ok(view);
    }

    [HttpGet("academic-calendar/terms")]
    [Authorize]
    public async Task<IActionResult> ListStudentTerms(CancellationToken ct)
        => Ok(await _svc.ListTermsAsync(ct));
}
