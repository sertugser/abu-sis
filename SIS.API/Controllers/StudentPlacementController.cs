using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Application.StudentCard.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/students/{studentId:int}/placement")]
[Authorize]
public class StudentPlacementController : ControllerBase
{
    private readonly IStudentPlacementService _placementService;
    private readonly AppDbContext _context;
    private readonly IAcademicScopeGuard _scopeGuard;

    public StudentPlacementController(
        IStudentPlacementService placementService,
        AppDbContext context,
        IAcademicScopeGuard scopeGuard)
    {
        _placementService = placementService;
        _context = context;
        _scopeGuard = scopeGuard;
    }

    [HttpGet("")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    public async Task<IActionResult> Get(int studentId, CancellationToken cancellationToken)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;

        var dto = await _placementService.GetAsync(studentId, cancellationToken);
        return dto == null ? NotFound(new { message = "Öğrenci bulunamadı." }) : Ok(dto);
    }

    [HttpPut("")]
    [RequirePermission("menu.ogrenci-rehberi.edit-placement")]
    public async Task<IActionResult> Upsert(int studentId, [FromBody] UpsertStudentPlacementDto dto, CancellationToken cancellationToken)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        try
        {
            await _placementService.UpsertAsync(studentId, dto, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
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
