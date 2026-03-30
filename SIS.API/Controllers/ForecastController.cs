using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.EnrollmentForecast.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "AdministrativeStaff,DepartmentManager,Dean,SystemAdministrator")]
public class ForecastController : ControllerBase
{
    private readonly IEnrollmentForecastService _forecastService;

    public ForecastController(IEnrollmentForecastService forecastService)
    {
        _forecastService = forecastService;
    }

    [HttpGet("course-demand")]
    public async Task<IActionResult> GetCourseDemand([FromQuery] int courseId)
    {
        if (courseId <= 0)
            return BadRequest(new { error = "Gecerli bir courseId gonderilmelidir." });

        var forecast = await _forecastService.GetCourseDemandForecastAsync(courseId);
        if (forecast == null)
            return NotFound(new { error = "Ders bulunamadi." });

        return Ok(forecast);
    }

    [HttpGet("department-summary")]
    public async Task<IActionResult> GetDepartmentSummary([FromQuery] int departmentId)
    {
        if (departmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        var forecast = await _forecastService.GetDepartmentSummaryAsync(departmentId);
        if (forecast == null)
            return NotFound(new { error = "Bolum bulunamadi." });

        return Ok(forecast);
    }
}
