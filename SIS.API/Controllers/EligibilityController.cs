using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.StudentEligibility.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "AdministrativeStaff,SystemAdministrator,DepartmentManager,Dean,AcademicAdvisor")]
public class EligibilityController : ControllerBase
{
    private readonly IStudentEligibilityService _eligibilityService;

    public EligibilityController(IStudentEligibilityService eligibilityService)
    {
        _eligibilityService = eligibilityService;
    }

    /// <summary>
    /// Belirli bir ders icin tum ogrencilerin uygunluk analizini dondurur.
    /// Uygun ve uygun olmayan ogrenciler neden bilgisiyle birlikte listelenir.
    /// </summary>
    [HttpGet("course/{courseId}")]
    public async Task<IActionResult> GetCourseEligibility(
        int courseId,
        [FromQuery] int? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        if (courseId <= 0)
            return BadRequest(new { error = "Gecerli bir courseId gonderilmelidir." });

        var report = await _eligibilityService.GetCourseEligibilityReportAsync(
            courseId, departmentId, cancellationToken);

        if (report == null)
            return NotFound(new { error = "Ders bulunamadi." });

        return Ok(report);
    }

    /// <summary>
    /// Belirli bir ogrencinin mufredatindaki tum derslere uygunluk analizini dondurur.
    /// </summary>
    [HttpGet("student/{studentId}")]
    [Authorize(Roles = "AdministrativeStaff,SystemAdministrator,DepartmentManager,Dean,AcademicAdvisor,Student")]
    public async Task<IActionResult> GetStudentEligibility(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0)
            return BadRequest(new { error = "Gecerli bir studentId gonderilmelidir." });

        var results = await _eligibilityService.GetStudentEligibleCoursesAsync(
            studentId, cancellationToken);

        return Ok(results);
    }
}
