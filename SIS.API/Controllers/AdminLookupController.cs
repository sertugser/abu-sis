using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminLookupController(AppDbContext db, ISystemConfigProvider systemConfig) : ControllerBase
{
    [HttpGet("faculties")]
    [RequirePermission("lookup.faculty")]
    public async Task<IActionResult> GetFaculties()
    {
        // Scope-aware: db.Faculties global query filter'ı sayesinde scope-bound roller
        // (Dean) yalnızca atanmış fakültelerini görür; diğer roller hepsini görür.
        var faculties = await db.Faculties
            .OrderBy(f => f.Name)
            .Select(f => new { f.Id, f.Name, f.Code })
            .ToListAsync();
        return Ok(faculties);
    }

    [HttpGet("departments")]
    [RequirePermission("lookup.department")]
    public async Task<IActionResult> GetDepartments([FromQuery] int? facultyId)
    {
        var query = db.Departments.AsQueryable();
        if (facultyId.HasValue)
            query = query.Where(d => d.FacultyId == facultyId.Value);

        var departments = await query
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name, d.Code, d.FacultyId })
            .ToListAsync();
        return Ok(departments);
    }

    [HttpGet("programs")]
    [RequirePermission("lookup.program")]
    public async Task<IActionResult> GetPrograms([FromQuery] int? departmentId)
    {
        var query = db.AcademicPrograms.AsQueryable();
        if (departmentId.HasValue)
            query = query.Where(p => p.DepartmentId == departmentId.Value);

        var programs = await query
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Code, p.DepartmentId })
            .ToListAsync();
        return Ok(programs);
    }

    [HttpGet("curricula")]
    [RequirePermission("lookup.curriculum")]
    public async Task<IActionResult> GetCurricula([FromQuery] int programId)
    {
        var curricula = await db.Curricula
            .AsNoTracking()
            .Where(c => c.ProgramId == programId)
            .OrderByDescending(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.ProgramId,
                Name = c.Name ?? c.Version,
                c.Version,
                c.Status,
            })
            .ToListAsync();
        return Ok(curricula);
    }

    [HttpGet("sections")]
    [RequirePermission("lookup.section")]
    public async Task<IActionResult> GetSections([FromQuery] int termId)
    {
        // Döneme göre şube listesi — rapor filtreleri için isim seçici ("DERSKODU - Şube N").
        var rows = await db.CourseSections
            .AsNoTracking()
            .Where(s => s.TermId == termId)
            .OrderBy(s => s.Course.Code).ThenBy(s => s.SectionNumber)
            .Select(s => new { s.Id, s.SectionNumber, CourseCode = s.Course.Code })
            .ToListAsync();
        var sections = rows.Select(s => new { s.Id, Label = $"{s.CourseCode} - Şube {s.SectionNumber}" });
        return Ok(sections);
    }

    [HttpGet("advisors")]
    [RequirePermission("lookup.advisor")]
    public async Task<IActionResult> GetAdvisors()
    {
        var enableAdvisorOptOutOptions = await systemConfig.GetBoolAsync("Instructor.EnableAdvisorOptOutOptions", true);

        var query = (
            from i in db.Instructors
            join u in db.Users on i.UserId equals u.Id
            join d in db.Departments on i.DepartmentId equals d.Id
            orderby u.LastName, u.FirstName
            select new
            {
                i.Id,
                i.AdvisorOptOutRequested,
                FullName = i.Title + " " + u.FirstName + " " + u.LastName,
                DepartmentName = d.Name,
            });

        if (enableAdvisorOptOutOptions)
            query = query.Where(item => !item.AdvisorOptOutRequested);

        var advisors = await query
            .Select(item => new
            {
                item.Id,
                item.FullName,
                item.DepartmentName,
            })
            .ToListAsync();

        return Ok(advisors);
    }
}
