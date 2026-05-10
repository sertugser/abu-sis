using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SIS.API.Authorization;
using SIS.Application.AcademicCalendar.DTOs;
using SIS.Application.AcademicCalendar.Services;
using SIS.Application.Identity.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api")]
public class AcademicCalendarController : ControllerBase
{
    private readonly IAcademicCalendarService _svc;
    private readonly IPermissionService _permissionService;

    public AcademicCalendarController(IAcademicCalendarService svc, IPermissionService permissionService)
    {
        _svc = svc;
        _permissionService = permissionService;
    }

    // Public (authenticated)

    [HttpGet("academic-calendar/active")]
    [Authorize]
    [RequirePermission("menu.akademik-takvim.view")]
    public async Task<IActionResult> GetActiveStudent([FromQuery] bool includeAllEvents = false, CancellationToken ct = default)
    {
        var view = await _svc.GetActiveStudentViewAsync(await CanIncludeAllEventsAsync(includeAllEvents, ct), ct);
        return view is null ? NotFound(new { message = "Aktif dönem bulunamadı." }) : Ok(view);
    }

    [HttpGet("academic-calendar/{termId:int}")]
    [Authorize]
    [RequirePermission("menu.akademik-takvim.view")]
    public async Task<IActionResult> GetStudent(int termId, [FromQuery] bool includeAllEvents = false, CancellationToken ct = default)
    {
        var view = await _svc.GetStudentViewAsync(termId, await CanIncludeAllEventsAsync(includeAllEvents, ct), ct);
        return view is null ? NotFound(new { message = "Dönem bulunamadı." }) : Ok(view);
    }

    [HttpGet("academic-calendar/terms")]
    [Authorize]
    [RequirePermission("lookup.term")]
    public async Task<IActionResult> ListStudentTerms(CancellationToken ct)
        => Ok(await _svc.ListTermsAsync(ct));

    [HttpPut("academic-calendar/{termId:int}/events/{eventId:int}")]
    [Authorize]
    [RequireAnyPermission("menu.akademik-takvim-olusturma.edit", "menu.akademik-takvim-olusturma.generate")]
    public async Task<IActionResult> UpdateEvent(int termId, int eventId, [FromBody] UpdateTermCalendarEventDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _svc.UpdateEventAsync(termId, eventId, dto, ct));
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

    [HttpPost("academic-calendar/{termId:int}/events/{eventId:int}/preview-move")]
    [Authorize]
    [RequireAnyPermission("menu.akademik-takvim-olusturma.edit", "menu.akademik-takvim-olusturma.generate")]
    public async Task<IActionResult> PreviewEventMove(int termId, int eventId, [FromBody] PreviewTermCalendarEventMoveDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _svc.PreviewEventMoveAsync(termId, eventId, dto, ct));
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

    [HttpPut("academic-calendar/events")]
    [Authorize]
    [RequireAnyPermission("menu.akademik-takvim-olusturma.edit", "menu.akademik-takvim-olusturma.generate")]
    public async Task<IActionResult> SaveEventChanges([FromBody] SaveAcademicCalendarChangesDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _svc.SaveEventChangesAsync(dto, ct));
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

    private async Task<bool> CanIncludeAllEventsAsync(bool requested, CancellationToken ct)
    {
        if (!requested)
            return false;

        if (User.IsInRole("SystemAdministrator"))
            return true;

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
            return false;

        return await _permissionService.HasPermissionAsync(userId, "menu.akademik-takvim-olusturma.edit", ct)
            || await _permissionService.HasPermissionAsync(userId, "menu.akademik-takvim-olusturma.generate", ct);
    }
}
