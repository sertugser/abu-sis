using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicProgression.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "DepartmentManager,Dean,SystemAdministrator")]
public class AcademicProgressionController : ControllerBase
{
    private readonly IAcademicProgressionService _progressionService;
    private readonly AppDbContext _context;

    public AcademicProgressionController(IAcademicProgressionService progressionService, AppDbContext context)
    {
        _progressionService = progressionService;
        _context = context;
    }

    [HttpGet("grade-lock-readiness/{departmentId}")]
    public async Task<IActionResult> CheckGradeLockReadiness(
        int departmentId,
        [FromQuery] int? termId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, cancellationToken)) return Forbid();

        var result = await _progressionService.CheckGradeLockReadinessAsync(
            departmentId, termId, cancellationToken);
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

        if (roles.Contains("SystemAdministrator"))
            return true;

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId, ct);

        if (instructor == null) return false;

        if (roles.Contains("Dean"))
        {
            var department = await _context.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == departmentId, ct);
            return department != null && department.FacultyId == instructor.Department.FacultyId;
        }

        if (roles.Contains("DepartmentManager"))
            return instructor.DepartmentId == departmentId;

        return false;
    }
}
