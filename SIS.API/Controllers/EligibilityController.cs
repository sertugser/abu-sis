using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.StudentEligibility.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EligibilityController : ControllerBase
{
    private readonly IStudentEligibilityService _eligibilityService;
    private readonly AppDbContext _context;

    public EligibilityController(IStudentEligibilityService eligibilityService, AppDbContext context)
    {
        _eligibilityService = eligibilityService;
        _context = context;
    }

    /// <summary>
    /// Belirli bir ders icin tum ogrencilerin uygunluk analizini dondurur.
    /// Uygun ve uygun olmayan ogrenciler neden bilgisiyle birlikte listelenir.
    /// </summary>
    [HttpGet("course/{courseId}")]
    // Patch 2A: Permission kaldırıldı; data multi-role (sadece authenticated).
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
    // Patch 2A: Permission kaldırıldı; data multi-role (sadece authenticated).
    public async Task<IActionResult> GetStudentEligibility(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0)
            return BadRequest(new { error = "Gecerli bir studentId gonderilmelidir." });

        // IDOR koruması: Student rolü sadece kendi verisini görebilir
        if (User.IsInRole("Student"))
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var ownStudentId = await _context.Students
                .Where(s => s.UserId == userId)
                .Select(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (ownStudentId == 0 || ownStudentId != studentId)
                return Forbid();
        }

        var results = await _eligibilityService.GetStudentEligibleCoursesAsync(
            studentId, cancellationToken);

        return Ok(results);
    }
}
