using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.AdvisorInfo.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/advisor-info")]
[Authorize]
public class AdvisorInfoController : ControllerBase
{
    private readonly IAdvisorInfoService _service;

    public AdvisorInfoController(IAdvisorInfoService service)
    {
        _service = service;
    }

    /// <summary>
    /// GET /api/advisor-info/me — Öğrencinin kendi danışmanının iletişim bilgileri.
    /// HasAdvisor=false ise diğer alanlar null döner.
    /// </summary>
    [HttpGet("me")]
    [RequirePermission("menu.danisman-bilgileri.view")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _service.GetMyAdvisorAsync(userId, ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/advisor-info/schedule — Öğrencinin danışmanının aktif dönem haftalık ders programı.
    /// Endpoint claim'den student id çözer — başkasının danışmanına erişim imkansız.
    /// </summary>
    [HttpGet("schedule")]
    [RequirePermission("menu.danisman-bilgileri.view")]
    public async Task<IActionResult> GetSchedule(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            var result = await _service.GetAdvisorScheduleAsync(userId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private bool TryGetUserId(out int userId)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out userId);
    }
}
