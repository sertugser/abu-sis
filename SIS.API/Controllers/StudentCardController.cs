using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/students/{studentId:int}")]
[Authorize]
public class StudentCardController : ControllerBase
{
    private readonly ICourseRegistrationService _registrationService;
    private readonly IGpaCalculationService _gpaService;
    private readonly AppDbContext _context;
    private readonly IAcademicScopeGuard _scopeGuard;

    public StudentCardController(
        ICourseRegistrationService registrationService,
        IGpaCalculationService gpaService,
        AppDbContext context,
        IAcademicScopeGuard scopeGuard)
    {
        _registrationService = registrationService;
        _gpaService = gpaService;
        _context = context;
        _scopeGuard = scopeGuard;
    }

    [HttpGet("registrations")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    public async Task<IActionResult> GetRegistrations(int studentId)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        return Ok(await _registrationService.GetStudentRegistrationsAsync(studentId));
    }

    [HttpGet("course-history")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    public async Task<IActionResult> GetCourseHistory(int studentId)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        return Ok(await _registrationService.GetCourseHistoryAsync(studentId));
    }

    [HttpGet("term-gpa")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    public async Task<IActionResult> GetTermGpa(int studentId)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        return Ok(await _gpaService.GetTermGpaHistoryAsync(studentId));
    }

    [HttpGet("attendance")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    public async Task<IActionResult> GetAttendance(int studentId, [FromQuery] int? termId)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        var status = await _registrationService.GetStudentAttendanceStatusAsync(studentId, termId);
        return status == null ? NotFound(new { message = "Devamsızlık verisi bulunamadı." }) : Ok(status);
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
