using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.Common.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/academic-calendar")]
public class AcademicCalendarController : ControllerBase
{
    private readonly IAcademicCalendarService _service;

    public AcademicCalendarController(IAcademicCalendarService service)
    {
        _service = service;
    }

    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActiveCalendar()
    {
        var result = await _service.GetActiveTermCalendarAsync();
        if (result is null)
            return NotFound(new { message = "Henüz akademik takvim oluşturucudan onaylanmış bir takvim yok." });
        return Ok(result);
    }
}
