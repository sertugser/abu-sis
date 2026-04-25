using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.TermClosing.DTOs;
using SIS.Application.TermClosing.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/term-closing")]
[Authorize]
[RequestTimeout(milliseconds: 300_000)] // 5 dakika — büyük bölümlerde not kilitleme + GPA uzun sürebilir
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
    [RequirePermission("menu.donem-kapatma.view")]
    public async Task<IActionResult> GetStatus(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _service.GetStatusAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpPost("{departmentId}/lock-grades")]
    [RequirePermission("menu.donem-kapatma.lock-grades")]
    public async Task<IActionResult> LockGrades(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        try
        {
            var result = await _service.LockGradesAsync(departmentId, userId, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{departmentId}/unlock-grades")]
    [RequirePermission("menu.donem-kapatma.unlock-grades")]
    public async Task<IActionResult> UnlockGrades(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        try
        {
            var result = await _service.UnlockGradesAsync(departmentId, userId, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{departmentId}/calculate-gpa")]
    [RequirePermission("menu.donem-kapatma.calculate-gpa")]
    public async Task<IActionResult> CalculateGpa(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        try
        {
            var result = await _service.CalculateGpaAsync(departmentId, userId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{departmentId}/preview-promote")]
    [RequirePermission("menu.donem-kapatma.view")]
    public async Task<IActionResult> PreviewPromotion(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        try
        {
            var result = await _service.PreviewPromotionAsync(departmentId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{departmentId}/promote")]
    [RequirePermission("menu.sinif-atlatma-islemleri.promote")]
    public async Task<IActionResult> Promote(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        try
        {
            var result = await _service.PromoteAsync(departmentId, userId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{departmentId}/rollback-promote")]
    [RequirePermission("menu.sinif-atlatma-islemleri.rollback")]
    public async Task<IActionResult> RollbackPromotion(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        try
        {
            var result = await _service.RollbackPromotionAsync(departmentId, userId, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{departmentId}/open-new-term")]
    [RequirePermission("menu.donem-kapatma.open-new-term")]
    public async Task<IActionResult> OpenNewTerm(int departmentId, [FromBody] NewTermRequestDto request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        try
        {
            var result = await _service.OpenNewTermAsync(departmentId, userId, request, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{departmentId}/grade-readiness-detail")]
    [RequirePermission("menu.donem-kapatma.view")]
    public async Task<IActionResult> GetGradeReadinessDetail(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _service.GetGradeReadinessDetailAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpGet("{departmentId}/grade-distribution")]
    [RequirePermission("menu.donem-kapatma.view")]
    public async Task<IActionResult> GetGradeDistribution(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _service.GetGradeDistributionAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpGet("overview")]
    [RequirePermission("menu.donem-kapatma.view")]
    public async Task<IActionResult> GetDepartmentOverview([FromQuery] int? facultyId, CancellationToken ct)
    {
        if (!TryGetUserId(out _)) return Unauthorized();

        // Sadece Dean ve SystemAdmin görebilir
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (!roles.Contains("Dean") && !roles.Contains("SystemAdministrator"))
            return Forbid();

        var result = await _service.GetDepartmentOverviewAsync(facultyId, ct);
        return Ok(result);
    }

    [HttpGet("{departmentId}/export-promotion")]
    [RequirePermission("menu.sinif-atlatma-islemleri.export")]
    public async Task<IActionResult> ExportPromotionReport(int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        try
        {
            var bytes = await _service.ExportPromotionReportAsync(departmentId, ct);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "sinif-atlatma-raporu.xlsx");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
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
