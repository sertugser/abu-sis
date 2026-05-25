using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Integration;
using SIS.Application.StudentCard.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/students/{studentId:int}/integration")]
[Authorize]
public class StudentIntegrationController : ControllerBase
{
    private readonly IExternalIdentityService _identity;
    private readonly IExternalStudentReportingService _reporting;
    private readonly AppDbContext _context;
    private readonly IAcademicScopeGuard _scopeGuard;

    public StudentIntegrationController(
        IExternalIdentityService identity,
        IExternalStudentReportingService reporting,
        AppDbContext context,
        IAcademicScopeGuard scopeGuard)
    {
        _identity = identity;
        _reporting = reporting;
        _context = context;
        _scopeGuard = scopeGuard;
    }

    [HttpPost("nvi/verify")]
    [RequirePermission("menu.ogrenci-rehberi.integration")]
    public async Task<IActionResult> VerifyIdentity(int studentId, CancellationToken cancellationToken)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        try
        {
            var result = await _identity.VerifyAsync(studentId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (NotImplementedException ex) { return StatusCode(501, new { message = ex.Message }); }
    }

    [HttpGet("yoksis/status")]
    [RequirePermission("menu.ogrenci-rehberi.integration")]
    public async Task<IActionResult> GetYoksisStatus(int studentId, CancellationToken cancellationToken)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        try
        {
            var result = await _reporting.GetStatusAsync(studentId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (NotImplementedException ex) { return StatusCode(501, new { message = ex.Message }); }
    }

    [HttpPost("yoksis/submit")]
    [RequirePermission("menu.ogrenci-rehberi.integration")]
    public async Task<IActionResult> SubmitYoksis(int studentId, CancellationToken cancellationToken)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        try
        {
            var result = await _reporting.SubmitAsync(studentId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (NotImplementedException ex) { return StatusCode(501, new { message = ex.Message }); }
    }

    /// <summary>
    /// Preview-only ÖSYM placement lookup. Returns the queried result without
    /// persisting it; saving is a separate, deliberate action through the
    /// placement upsert endpoint.
    /// </summary>
    [HttpPost("osym/fetch-placement")]
    [RequirePermission("menu.ogrenci-rehberi.integration")]
    public async Task<IActionResult> FetchPlacement(int studentId, CancellationToken cancellationToken)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        try
        {
            var result = await _reporting.QueryPlacementAsync(studentId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (NotImplementedException ex) { return StatusCode(501, new { message = ex.Message }); }
    }

    private async Task<IActionResult?> EnsureAccessAsync(int studentId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim == null || !int.TryParse(claim, out var userId)) return Unauthorized();

        var deptId = await _context.StudentPrograms
            .AsNoTracking()
            .Include(sp => sp.Program)
            .Where(sp => sp.StudentId == studentId && sp.IsActive && sp.Type == StudentProgramType.Main)
            .Select(sp => (int?)sp.Program.DepartmentId)
            .FirstOrDefaultAsync();

        if (deptId == null) return NotFound(new { message = "Öğrenci bulunamadı." });
        if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, deptId.Value)) return Forbid();
        return null;
    }
}
