using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.TermClosing.DTOs;
using SIS.Application.TermClosing.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/term-closing")]
[Authorize(Roles = "DepartmentManager,Dean,SystemAdministrator")]
public class TermClosingController : ControllerBase
{
    private readonly ITermClosingService _service;
    private readonly AppDbContext _context;

    public TermClosingController(ITermClosingService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    [HttpGet("{departmentId}/status")]
    public async Task<IActionResult> GetStatus(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _service.GetStatusAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpPost("{departmentId}/lock-grades")]
    public async Task<IActionResult> LockGrades(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _service.LockGradesAsync(departmentId, userId, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{departmentId}/unlock-grades")]
    public async Task<IActionResult> UnlockGrades(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _service.UnlockGradesAsync(departmentId, userId, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{departmentId}/calculate-gpa")]
    public async Task<IActionResult> CalculateGpa(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _service.CalculateGpaAsync(departmentId, userId, ct);
        return Ok(result);
    }

    [HttpGet("{departmentId}/preview-promote")]
    public async Task<IActionResult> PreviewPromotion(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _service.PreviewPromotionAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpPost("{departmentId}/promote")]
    public async Task<IActionResult> Promote(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _service.PromoteAsync(departmentId, userId, ct);
        return Ok(result);
    }

    [HttpPost("{departmentId}/rollback-promote")]
    public async Task<IActionResult> RollbackPromotion(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _service.RollbackPromotionAsync(departmentId, userId, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{departmentId}/open-new-term")]
    public async Task<IActionResult> OpenNewTerm(int departmentId, [FromBody] NewTermRequestDto request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _service.OpenNewTermAsync(departmentId, userId, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
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
