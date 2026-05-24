using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.StudentCard.Services;
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
    private readonly IStudentTranscriptService _transcriptService;
    private readonly ICurriculumStatusService _curriculumStatusService;

    public StudentCardController(
        ICourseRegistrationService registrationService,
        IGpaCalculationService gpaService,
        AppDbContext context,
        IAcademicScopeGuard scopeGuard,
        IStudentTranscriptService transcriptService,
        ICurriculumStatusService curriculumStatusService)
    {
        _registrationService = registrationService;
        _gpaService = gpaService;
        _context = context;
        _scopeGuard = scopeGuard;
        _transcriptService = transcriptService;
        _curriculumStatusService = curriculumStatusService;
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

    [HttpGet("transcript")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    public async Task<IActionResult> GetTranscript(int studentId)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        var dto = await _transcriptService.GetTranscriptAsync(studentId);
        return dto == null ? NotFound(new { message = "Transkript bulunamadı." }) : Ok(dto);
    }

    [HttpGet("curriculum-status")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    public async Task<IActionResult> GetCurriculumStatus(int studentId)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        var dto = await _curriculumStatusService.GetCurriculumStatusAsync(studentId);
        return dto == null ? NotFound(new { message = "Müfredat bilgisi bulunamadı." }) : Ok(dto);
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
