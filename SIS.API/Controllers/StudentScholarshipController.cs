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
[Route("api/admin/students/{studentId:int}/scholarships")]
[Authorize]
public class StudentScholarshipController : ControllerBase
{
    private readonly IStudentScholarshipService _scholarshipService;
    private readonly AppDbContext _context;
    private readonly IAcademicScopeGuard _scopeGuard;

    public StudentScholarshipController(
        IStudentScholarshipService scholarshipService,
        AppDbContext context,
        IAcademicScopeGuard scopeGuard)
    {
        _scholarshipService = scholarshipService;
        _context = context;
        _scopeGuard = scopeGuard;
    }

    [HttpGet("")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    public async Task<IActionResult> Get(int studentId, CancellationToken cancellationToken)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;

        var list = await _scholarshipService.GetForStudentAsync(studentId, cancellationToken);
        return list == null ? NotFound(new { message = "Öğrenci bulunamadı." }) : Ok(list);
    }

    [HttpPost("")]
    [RequirePermission("menu.ogrenci-rehberi.edit-scholarship")]
    public async Task<IActionResult> Create(int studentId, [FromBody] UpsertScholarshipDetailDto dto, CancellationToken cancellationToken)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        try
        {
            var id = await _scholarshipService.CreateAsync(studentId, dto, cancellationToken);
            return CreatedAtAction(nameof(Get), new { studentId, id }, new { id });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.ogrenci-rehberi.edit-scholarship")]
    public async Task<IActionResult> Update(int studentId, int id, [FromBody] UpsertScholarshipDetailDto dto, CancellationToken cancellationToken)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        try
        {
            await _scholarshipService.UpdateAsync(studentId, id, dto, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("menu.ogrenci-rehberi.edit-scholarship")]
    public async Task<IActionResult> Delete(int studentId, int id, CancellationToken cancellationToken)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        try
        {
            await _scholarshipService.DeleteAsync(studentId, id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
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
