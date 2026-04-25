using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

public record CourseAccessPolicyDto(int Id, int CourseId, string CourseCode, int TermId, int? FacultyId, string? FacultyName, int? DepartmentId, string? DepartmentName);
public record CreateCourseAccessPolicyDto(int CourseId, int TermId, int? FacultyId, int? DepartmentId);

[ApiController]
[Route("api/course-access-policy")]
[Authorize]
public class CourseAccessPolicyController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    [HttpGet]
    [RequirePermission("menu.ders-katalogu.access-policy.view")]
    public async Task<IActionResult> GetPolicies([FromQuery] int courseId, [FromQuery] int termId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasCourseAccessAsync(userId, courseId, ct)) return Forbid();

        var policies = await _context.CourseAccessPolicies
            .AsNoTracking()
            .Include(p => p.Course)
            .Include(p => p.Faculty)
            .Include(p => p.Department)
            .Where(p => p.CourseId == courseId && p.TermId == termId)
            .OrderBy(p => p.Id)
            .Select(p => new CourseAccessPolicyDto(
                p.Id, p.CourseId, p.Course.Code, p.TermId,
                p.FacultyId, p.Faculty != null ? p.Faculty.Name : null,
                p.DepartmentId, p.Department != null ? p.Department.Name : null))
            .ToListAsync(ct);

        return Ok(policies);
    }

    [HttpPost]
    [RequirePermission("menu.ders-katalogu.access-policy.create")]
    public async Task<IActionResult> Create([FromBody] CreateCourseAccessPolicyDto dto, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasCourseAccessAsync(userId, dto.CourseId, ct)) return Forbid();

        if (dto.FacultyId == null && dto.DepartmentId == null)
            return BadRequest(new { error = "FacultyId veya DepartmentId'den en az biri belirtilmelidir." });

        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.CourseId, ct);
        if (course == null)
            return NotFound(new { error = "Ders bulunamadı." });

        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == dto.TermId, ct);
        if (term == null)
            return NotFound(new { error = "Dönem bulunamadı." });

        if (dto.FacultyId != null)
        {
            var faculty = await _context.Faculties.AsNoTracking().FirstOrDefaultAsync(f => f.Id == dto.FacultyId, ct);
            if (faculty == null)
                return NotFound(new { error = "Fakülte bulunamadı." });
        }

        if (dto.DepartmentId != null)
        {
            var department = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dto.DepartmentId, ct);
            if (department == null)
                return NotFound(new { error = "Bölüm bulunamadı." });
        }

        // Aynı policy zaten var mı kontrol et
        var exists = await _context.CourseAccessPolicies.AnyAsync(p =>
            p.CourseId == dto.CourseId && p.TermId == dto.TermId &&
            p.FacultyId == dto.FacultyId && p.DepartmentId == dto.DepartmentId, ct);

        if (exists)
            return BadRequest(new { error = "Bu erişim politikası zaten tanımlı." });

        var policy = new CourseAccessPolicy
        {
            CourseId = dto.CourseId,
            TermId = dto.TermId,
            FacultyId = dto.FacultyId,
            DepartmentId = dto.DepartmentId
        };

        _context.CourseAccessPolicies.Add(policy);
        await _context.SaveChangesAsync(ct);

        // Reload with navigation properties for response
        var created = await _context.CourseAccessPolicies
            .AsNoTracking()
            .Include(p => p.Course)
            .Include(p => p.Faculty)
            .Include(p => p.Department)
            .Where(p => p.Id == policy.Id)
            .Select(p => new CourseAccessPolicyDto(
                p.Id, p.CourseId, p.Course.Code, p.TermId,
                p.FacultyId, p.Faculty != null ? p.Faculty.Name : null,
                p.DepartmentId, p.Department != null ? p.Department.Name : null))
            .FirstAsync(ct);

        return CreatedAtAction(null, null, created);
    }

    [HttpDelete("{id}")]
    [RequirePermission("menu.ders-katalogu.access-policy.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var policy = await _context.CourseAccessPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy == null)
            return NotFound(new { error = "Erişim politikası bulunamadı." });

        if (!await HasCourseAccessAsync(userId, policy.CourseId, ct)) return Forbid();

        _context.CourseAccessPolicies.Remove(policy);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out userId);
    }

    private async Task<bool> HasCourseAccessAsync(int userId, int courseId, CancellationToken ct)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        if (roles.Contains("SystemAdministrator"))
            return true;

        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId, ct);
        if (course == null) return false;

        // Ortak ders (DepartmentId null) — sadece SystemAdmin yönetebilir
        if (course.DepartmentId == null) return false;

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId, ct);

        if (instructor == null) return false;

        if (roles.Contains("Dean"))
        {
            var department = await _context.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == course.DepartmentId, ct);
            return department != null && department.FacultyId == instructor.Department.FacultyId;
        }

        if (roles.Contains("DepartmentManager"))
            return instructor.DepartmentId == course.DepartmentId;

        return false;
    }
}
