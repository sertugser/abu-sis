using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.OpenedDepartmentCourses.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/opened-department-courses")]
[Authorize]
public class OpenedDepartmentCoursesController : ControllerBase
{
    private readonly IOpenedDepartmentCourseService _service;

    public OpenedDepartmentCoursesController(IOpenedDepartmentCourseService service)
    {
        _service = service;
    }

    /// <summary>
    /// GET /api/opened-department-courses/me — Öğrencinin bölümünde aktif dönemde açılmış section'lar.
    /// Sınıftan bağımsız (1-4 hepsi). Endpoint claim'den student id çözer.
    /// </summary>
    [HttpGet("me")]
    [RequirePermission("menu.acilan-bolum-dersleri.view")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _service.GetForStudentAsync(userId, ct);
        return Ok(result);
    }

    private bool TryGetUserId(out int userId)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out userId);
    }
}
