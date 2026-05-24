using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.AcademicProgression.DTOs;
using SIS.Application.AcademicProgression.Services;
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
    private readonly IAcademicProgressionService _progressionService;

    public StudentCardController(
        ICourseRegistrationService registrationService,
        IGpaCalculationService gpaService,
        AppDbContext context,
        IAcademicScopeGuard scopeGuard,
        IStudentTranscriptService transcriptService,
        ICurriculumStatusService curriculumStatusService,
        IAcademicProgressionService progressionService)
    {
        _registrationService = registrationService;
        _gpaService = gpaService;
        _context = context;
        _scopeGuard = scopeGuard;
        _transcriptService = transcriptService;
        _curriculumStatusService = curriculumStatusService;
        _progressionService = progressionService;
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

    [HttpGet("promotion-status")]
    [RequirePermission("menu.sinif-atlatma-islemleri.view")]
    public async Task<IActionResult> GetPromotionStatus(int studentId)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        var dto = await _progressionService.EvaluateStudentPromotionAsync(studentId);
        return dto == null ? NotFound(new { message = "Öğrenci bulunamadı." }) : Ok(dto);
    }

    [HttpPost("promote")]
    [RequirePermission("menu.sinif-atlatma-islemleri.promote")]
    public async Task<IActionResult> Promote(int studentId, [FromBody] PromoteStudentRequest req)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        try
        {
            var status = await _progressionService.EvaluateStudentPromotionAsync(studentId);
            if (status == null) return NotFound(new { message = "Öğrenci bulunamadı." });
            int target;
            if (req.TargetLevel.HasValue)      // manual override to a chosen level
                target = req.TargetLevel.Value;
            else                               // rule-based
            {
                if (!status.CanPromote) return BadRequest(new { message = status.Reason ?? "Öğrenci sınıf atlatma kurallarını sağlamıyor." });
                target = status.EligibleTargetLevel ?? status.CurrentLevel;
            }
            await _progressionService.SetStudentLevelAsync(studentId, target);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("promotion-rollback")]
    [RequirePermission("menu.sinif-atlatma-islemleri.rollback")]
    public async Task<IActionResult> RollbackPromotion(int studentId)
    {
        var guard = await EnsureAccessAsync(studentId);
        if (guard != null) return guard;
        try
        {
            var status = await _progressionService.EvaluateStudentPromotionAsync(studentId);
            if (status == null) return NotFound(new { message = "Öğrenci bulunamadı." });
            if (status.CurrentLevel <= 1) return BadRequest(new { message = "Öğrenci zaten 1. sınıfta; geri alınamaz." });
            await _progressionService.SetStudentLevelAsync(studentId, status.CurrentLevel - 1);
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
