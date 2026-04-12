using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseOpening.DTOs;
using SIS.Application.CourseOpening.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/course-opening")]
[Authorize(Roles = "DepartmentManager,Dean,AdministrativeStaff,SystemAdministrator")]
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
    public async Task<IActionResult> GetDepartmentInstructors([FromQuery] int departmentId, [FromQuery] int? termId = null, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _courseOpeningService.GetDepartmentInstructorsAsync(departmentId, termId, ct);
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

    [HttpPost("bulk-assign-instructor")]
    public async Task<IActionResult> BulkAssignInstructor(
        [FromQuery] int departmentId, [FromQuery] int termId,
        [FromBody] BulkAssignInstructorRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _courseOpeningService.BulkAssignInstructorAsync(departmentId, termId, request, ct);
        return Ok(result);
    }

    [HttpGet("instructors/{instructorId}/workload")]
    public async Task<IActionResult> GetInstructorWorkload(int instructorId, [FromQuery] int termId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var instructor = await _context.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instructorId, ct);

        if (instructor == null)
            return NotFound(new { error = "Öğretim üyesi bulunamadı." });

        if (!await HasDepartmentAccessAsync(userId, instructor.DepartmentId, ct))
            return Forbid();

        // Get instructor's user name
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == instructor.UserId, ct);

        var fullName = user != null
            ? $"{instructor.Title} {user.FirstName} {user.LastName}"
            : $"{instructor.Title} (#{instructor.Id})";

        // Get assigned sections for this term
        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.InstructorId == instructorId && cs.TermId == termId && cs.IsActive)
            .OrderBy(cs => cs.Course.Code).ThenBy(cs => cs.SectionNumber)
            .ToListAsync(ct);

        var assignedSections = sections.Select(cs => new AssignedSectionDto
        {
            SectionId = cs.Id,
            CourseCode = cs.Course.Code,
            CourseName = cs.Course.Name,
            SectionNumber = cs.SectionNumber,
            WeeklyHours = cs.Course.TheoryHours + cs.Course.LabHours,
            Capacity = cs.Capacity,
            EnrolledCount = cs.EnrolledCount,
        }).ToList();

        var assignedWeeklyHours = assignedSections.Sum(s => s.WeeklyHours);

        return Ok(new InstructorWorkloadDto
        {
            InstructorId = instructor.Id,
            FullName = fullName,
            Title = instructor.Title,
            MaxWeeklyHours = instructor.MaxWeeklyHours,
            AssignedWeeklyHours = assignedWeeklyHours,
            RemainingCapacity = Math.Max(0, instructor.MaxWeeklyHours - assignedWeeklyHours),
            AssignedSections = assignedSections,
        });
    }

    [HttpGet("previous-term-courses")]
    public async Task<IActionResult> GetPreviousTermCourses(
        [FromQuery] int departmentId, [FromQuery] int sourceTermId, [FromQuery] int targetTermId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _courseOpeningService.GetPreviousTermCoursesAsync(departmentId, sourceTermId, targetTermId, ct);
        return Ok(result);
    }

    [HttpPost("transfer-from-term")]
    public async Task<IActionResult> TransferFromTerm([FromBody] TransferFromTermRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, request.DepartmentId, ct)) return Forbid();

        var result = await _courseOpeningService.TransferFromTermAsync(request, ct);
        return Ok(result);
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

        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
            return true;

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId, ct);

        if (instructor == null) return false;

        // Dean: kendi fakültesindeki tüm bölümlere erişebilir
        if (roles.Contains("Dean"))
        {
            var department = await _context.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == departmentId, ct);
            return department != null && department.FacultyId == instructor.Department.FacultyId;
        }

        // DepartmentManager: sadece kendi bölümü
        if (roles.Contains("DepartmentManager"))
            return instructor.DepartmentId == departmentId;

        return false;
    }
}
