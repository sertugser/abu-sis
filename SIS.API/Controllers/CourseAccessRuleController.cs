using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/access-rules")]
[Authorize(Roles = "DepartmentManager,Dean,AdministrativeStaff,SystemAdministrator")]
public class CourseAccessRuleController(AppDbContext context) : ControllerBase
{
    public record CourseAccessRuleDto(
        int Id, int CourseId,
        int? AllowedFacultyId, string? AllowedFacultyName,
        int? AllowedDepartmentId, string? AllowedDepartmentName,
        int? AllowedProgramId, string? AllowedProgramName,
        int? MinClassYear, int? MaxClassYear,
        decimal? MinCgpa, bool RequiresAdvisorApproval,
        bool IsOpenToOwnDepartment);

    public record CreateAccessRuleRequest(
        int? AllowedFacultyId = null, int? AllowedDepartmentId = null,
        int? AllowedProgramId = null,
        int? MinClassYear = null, int? MaxClassYear = null,
        decimal? MinCgpa = null,
        bool RequiresAdvisorApproval = false,
        bool IsOpenToOwnDepartment = true);

    public record UpdateAccessRuleRequest(
        int? AllowedFacultyId = null, int? AllowedDepartmentId = null,
        int? AllowedProgramId = null,
        int? MinClassYear = null, int? MaxClassYear = null,
        decimal? MinCgpa = null,
        bool? RequiresAdvisorApproval = null,
        bool? IsOpenToOwnDepartment = null);

    [HttpGet]
    public async Task<IActionResult> List(int courseId, CancellationToken ct)
    {
        var courseExists = await context.Courses.AnyAsync(c => c.Id == courseId, ct);
        if (!courseExists)
            return NotFound(new { message = "Ders bulunamadı." });

        var rules = await context.CourseAccessRules
            .AsNoTracking()
            .Where(r => r.CourseId == courseId)
            .Include(r => r.AllowedFaculty)
            .Include(r => r.AllowedDepartment)
            .Include(r => r.AllowedProgram)
            .Select(r => new CourseAccessRuleDto(
                r.Id, r.CourseId,
                r.AllowedFacultyId, r.AllowedFaculty != null ? r.AllowedFaculty.Name : null,
                r.AllowedDepartmentId, r.AllowedDepartment != null ? r.AllowedDepartment.Name : null,
                r.AllowedProgramId, r.AllowedProgram != null ? r.AllowedProgram.Name : null,
                r.MinClassYear, r.MaxClassYear,
                r.MinCgpa, r.RequiresAdvisorApproval,
                r.IsOpenToOwnDepartment))
            .ToListAsync(ct);

        return Ok(rules);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int courseId, [FromBody] CreateAccessRuleRequest req, CancellationToken ct)
    {
        var courseExists = await context.Courses.AnyAsync(c => c.Id == courseId, ct);
        if (!courseExists)
            return NotFound(new { message = "Ders bulunamadı." });

        var entity = new Domain.Entities.CourseAccessRule
        {
            CourseId = courseId,
            AllowedFacultyId = req.AllowedFacultyId,
            AllowedDepartmentId = req.AllowedDepartmentId,
            AllowedProgramId = req.AllowedProgramId,
            MinClassYear = req.MinClassYear,
            MaxClassYear = req.MaxClassYear,
            MinCgpa = req.MinCgpa,
            RequiresAdvisorApproval = req.RequiresAdvisorApproval,
            IsOpenToOwnDepartment = req.IsOpenToOwnDepartment,
            CreatedAt = DateTime.UtcNow
        };

        context.CourseAccessRules.Add(entity);
        await context.SaveChangesAsync(ct);

        return Ok(new CourseAccessRuleDto(
            entity.Id, entity.CourseId,
            entity.AllowedFacultyId, null,
            entity.AllowedDepartmentId, null,
            entity.AllowedProgramId, null,
            entity.MinClassYear, entity.MaxClassYear,
            entity.MinCgpa, entity.RequiresAdvisorApproval,
            entity.IsOpenToOwnDepartment));
    }

    [HttpPut("{ruleId:int}")]
    public async Task<IActionResult> Update(int courseId, int ruleId, [FromBody] UpdateAccessRuleRequest req, CancellationToken ct)
    {
        var rule = await context.CourseAccessRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.CourseId == courseId, ct);
        if (rule is null)
            return NotFound(new { message = "Erişim kuralı bulunamadı." });

        if (req.AllowedFacultyId.HasValue) rule.AllowedFacultyId = req.AllowedFacultyId;
        if (req.AllowedDepartmentId.HasValue) rule.AllowedDepartmentId = req.AllowedDepartmentId;
        if (req.AllowedProgramId.HasValue) rule.AllowedProgramId = req.AllowedProgramId;
        if (req.MinClassYear.HasValue) rule.MinClassYear = req.MinClassYear;
        if (req.MaxClassYear.HasValue) rule.MaxClassYear = req.MaxClassYear;
        if (req.MinCgpa.HasValue) rule.MinCgpa = req.MinCgpa;
        if (req.RequiresAdvisorApproval.HasValue) rule.RequiresAdvisorApproval = req.RequiresAdvisorApproval.Value;
        if (req.IsOpenToOwnDepartment.HasValue) rule.IsOpenToOwnDepartment = req.IsOpenToOwnDepartment.Value;

        rule.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return Ok(new { message = "Erişim kuralı güncellendi." });
    }

    [HttpDelete("{ruleId:int}")]
    public async Task<IActionResult> Delete(int courseId, int ruleId, CancellationToken ct)
    {
        var rule = await context.CourseAccessRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.CourseId == courseId, ct);
        if (rule is null)
            return NotFound(new { message = "Erişim kuralı bulunamadı." });

        context.CourseAccessRules.Remove(rule);
        await context.SaveChangesAsync(ct);

        return NoContent();
    }
}
