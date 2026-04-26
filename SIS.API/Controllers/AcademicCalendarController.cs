using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.AcademicCalendar.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api")]
public class AcademicCalendarController : ControllerBase
{
    private readonly IAcademicCalendarService _svc;

    public AcademicCalendarController(IAcademicCalendarService svc) { _svc = svc; }

    // Public (authenticated)

    [HttpGet("academic-calendar/active")]
    [Authorize]
    [RequirePermission("menu.akademik-takvim.view")]
    public async Task<IActionResult> GetActiveStudent(CancellationToken ct)
    {
        var view = await _svc.GetActiveStudentViewAsync(ct);
        return view is null ? NotFound(new { message = "Aktif dönem bulunamadı." }) : Ok(view);
    }

    [HttpGet("academic-calendar/{termId:int}")]
    [Authorize]
    [RequirePermission("menu.akademik-takvim.view")]
    public async Task<IActionResult> GetStudent(int termId, CancellationToken ct)
    {
        var view = await _svc.GetStudentViewAsync(termId, ct);
        return view is null ? NotFound(new { message = "Dönem bulunamadı." }) : Ok(view);
    }

    [HttpGet("academic-calendar/terms")]
    [Authorize]
    [RequirePermission("lookup.term")]
    public async Task<IActionResult> ListStudentTerms(CancellationToken ct)
        => Ok(await _svc.ListTermsAsync(ct));
}
