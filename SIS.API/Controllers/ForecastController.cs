using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.EnrollmentForecast.DTOs;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ForecastController : ControllerBase
{
    private readonly IEnrollmentForecastService _forecastService;
    private readonly AppDbContext _context;

    public ForecastController(IEnrollmentForecastService forecastService, AppDbContext context)
    {
        _forecastService = forecastService;
        _context = context;
    }

    [HttpGet("course-demand")]
    [RequirePermission("menu.akilli-planlama.forecast")]
    public async Task<IActionResult> GetCourseDemand([FromQuery] int courseId, [FromQuery] bool includeTrace = false)
    {
        if (courseId <= 0)
            return BadRequest(new { error = "Gecerli bir courseId gonderilmelidir." });

        // Dersin bölümüne erişim kontrolü
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId);
        if (course == null)
            return NotFound(new { error = "Ders bulunamadi." });
        if (course.DepartmentId.HasValue && !await HasDepartmentAccessAsync(userId, course.DepartmentId.Value))
            return Forbid();

        var forecast = await _forecastService.GetCourseDemandForecastAsync(courseId);
        if (forecast == null)
            return NotFound(new { error = "Ders bulunamadi." });

        // Trace varsayılan kapalı — sadece istendiğinde döner
        if (!includeTrace)
            forecast.Trace = null;

        return Ok(forecast);
    }

    [HttpGet("department-summary")]
    [RequirePermission("menu.akilli-planlama.forecast")]
    public async Task<IActionResult> GetDepartmentSummary([FromQuery] int departmentId)
    {
        if (departmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId)) return Forbid();

        var forecast = await _forecastService.GetDepartmentSummaryAsync(departmentId);
        if (forecast == null)
            return NotFound(new { error = "Bolum bulunamadi." });

        // Toplu sorgularda trace strip — performans
        foreach (var summary in forecast.CourseSummaries)
            summary.Trace = null;

        return Ok(forecast);
    }

    [HttpPost("simulate")]
    [RequirePermission("menu.akilli-planlama.simulate")]
    public async Task<IActionResult> SimulateCourseDemand([FromQuery] int courseId, [FromBody] ForecastSimulationParams parameters)
    {
        if (courseId <= 0)
            return BadRequest(new { error = "Gecerli bir courseId gonderilmelidir." });

        if (!TryGetUserId(out var userId)) return Unauthorized();
        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId);
        if (course == null)
            return NotFound(new { error = "Ders bulunamadi." });
        if (course.DepartmentId.HasValue && !await HasDepartmentAccessAsync(userId, course.DepartmentId.Value))
            return Forbid();

        var forecast = await _forecastService.SimulateCourseDemandAsync(courseId, parameters);
        if (forecast == null)
            return NotFound(new { error = "Ders bulunamadi." });

        return Ok(forecast);
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out userId);
    }

    private async Task<bool> HasDepartmentAccessAsync(int userId, int departmentId)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
            return true;

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId);

        if (instructor == null) return false;

        if (roles.Contains("Dean"))
        {
            var department = await _context.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == departmentId);
            return department != null && department.FacultyId == instructor.Department.FacultyId;
        }

        if (roles.Contains("DepartmentManager"))
            return instructor.DepartmentId == departmentId;

        return false;
    }
}
