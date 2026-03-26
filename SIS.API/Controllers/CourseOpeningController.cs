using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseOpening.DTOs;
using SIS.Application.CourseOpening.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/course-opening")]
[Authorize(Roles = "DepartmentManager,AdministrativeStaff,SystemAdministrator")]
public class CourseOpeningController(ICourseOpeningService courseOpeningService, AppDbContext context) : ControllerBase
{
    private readonly ICourseOpeningService _courseOpeningService = courseOpeningService;
    private readonly AppDbContext _context = context;

    [HttpGet("list")]
    public async Task<IActionResult> GetOpenedCourses([FromQuery] int departmentId, [FromQuery] int termId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _courseOpeningService.GetOpenedCoursesAsync(departmentId, termId, ct);
        return Ok(result);
    }

    [HttpGet("curriculum-courses")]
    public async Task<IActionResult> GetCurriculumCoursesForOpening([FromQuery] int departmentId, [FromQuery] int termId, [FromQuery] bool showAll = false, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _courseOpeningService.GetCurriculumCoursesForOpeningAsync(departmentId, termId, showAll, ct);
        return Ok(result);
    }

    [HttpGet("instructors")]
    public async Task<IActionResult> GetDepartmentInstructors([FromQuery] int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _courseOpeningService.GetDepartmentInstructorsAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpPost("open")]
    public async Task<IActionResult> OpenCourse([FromQuery] int departmentId, [FromQuery] int termId, [FromBody] OpenCourseRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        try
        {
            var result = await _courseOpeningService.OpenCourseAsync(departmentId, termId, request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("bulk-open")]
    public async Task<IActionResult> BulkOpenCourses([FromQuery] int departmentId, [FromQuery] int termId, [FromBody] BulkOpenCoursesRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _courseOpeningService.BulkOpenCoursesAsync(departmentId, termId, request, ct);
        return Ok(result);
    }

    [HttpPut("section")]
    public async Task<IActionResult> UpdateSection([FromBody] UpdateOpenedSectionRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        // Verify department access via section's course
        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, ct);

        if (section == null) return NotFound(new { error = "Şube bulunamadı." });
        if (section.Course.DepartmentId == null) return BadRequest(new { error = "Ders bir bölüme ait değil." });
        if (!await HasDepartmentAccessAsync(userId, section.Course.DepartmentId.Value, ct)) return Forbid();

        try
        {
            var result = await _courseOpeningService.UpdateSectionAsync(request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("section/{id}")]
    public async Task<IActionResult> CloseSection(int id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == id, ct);

        if (section == null) return NotFound(new { error = "Şube bulunamadı." });
        if (section.Course.DepartmentId == null) return BadRequest(new { error = "Ders bir bölüme ait değil." });
        if (!await HasDepartmentAccessAsync(userId, section.Course.DepartmentId.Value, ct)) return Forbid();

        try
        {
            await _courseOpeningService.CloseSectionAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out userId);
    }

    private async Task<bool> HasDepartmentAccessAsync(int userId, int departmentId, CancellationToken ct)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        // SystemAdministrator and AdministrativeStaff can access all departments
        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
            return true;

        // DepartmentManager can only access their own department
        if (roles.Contains("DepartmentManager"))
        {
            var instructor = await _context.Instructors
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.UserId == userId, ct);
            return instructor != null && instructor.DepartmentId == departmentId;
        }

        return false;
    }
}
