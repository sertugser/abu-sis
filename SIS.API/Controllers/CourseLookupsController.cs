using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.DTOs;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/course-lookups")]
public class CourseLookupsController(AppDbContext context) : ControllerBase
{
    [HttpGet("opening-reasons")]
    [RequirePermission("menu.mufredat-islemleri.view")]
    public async Task<IActionResult> GetOpeningReasons(CancellationToken ct)
    {
        var list = await context.CurriculumCourseOpeningReasons
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
            .Select(r => new CurriculumCourseOpeningReasonDto(
                r.Id, r.Code, r.Name, r.Description,
                r.SortOrder, r.IsActive,
                r.AllowsAdvisorGrading, r.BypassScheduleConflict, r.BypassMandatoryAttendance,
                r.IsExcludedFromGpa, r.IsCreditFree, r.RequiresApproval, r.AllowsCustomGradingScale))
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("categories")]
    [RequirePermission("menu.mufredat-islemleri.view")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var list = await context.CourseCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CourseCategoryDto(c.Id, c.Code, c.Name, c.SortOrder, c.IsActive))
            .ToListAsync(ct);
        return Ok(list);
    }
}
