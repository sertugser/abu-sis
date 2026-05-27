using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/advisor")]
[Authorize]
public class AdvisorController : ControllerBase
{
    private readonly ICourseRegistrationService _registrationService;
    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _systemConfig;

    public AdvisorController(ICourseRegistrationService registrationService, AppDbContext context, ISystemConfigProvider systemConfig)
    {
        _registrationService = registrationService;
        _context = context;
        _systemConfig = systemConfig;
    }

    [HttpGet("pending-approvals")]
    [RequirePermission("menu.ders-kayit-duzenleme.view")]
    public async Task<IActionResult> GetPendingApprovals()
    {
        var calculationMethod = await _systemConfig.GetStringAsync("Instructor.AdvisingCalculationMethod", "standard");
        var instructorId = await GetInstructorIdAsync();
        if (instructorId == null)
            // Ogretim uyesi profili olmayan kullanicilar (orn. SystemAdmin) icin bos liste don.
            // 401 doner isek frontend axios interceptor JWT expired sanip logout yapar.
            return Ok(new PendingApprovalListDto
            {
                CalculationMethod = calculationMethod,
                CalculationMethodLabel = ResolveCalculationMethodLabel(calculationMethod),
                Items = new List<PendingApprovalDto>(),
            });

        var result = await _registrationService.GetPendingApprovalsAsync(instructorId.Value);
        return Ok(new PendingApprovalListDto
        {
            CalculationMethod = calculationMethod,
            CalculationMethodLabel = ResolveCalculationMethodLabel(calculationMethod),
            Items = result,
        });
    }

    [HttpGet("students")]
    [RequirePermission("menu.ders-kayit-duzenleme.view")]
    public async Task<IActionResult> GetAdvisorStudents(CancellationToken cancellationToken = default)
    {
        var instructorId = await GetInstructorIdAsync();
        if (instructorId == null)
            // Ogretim uyesi profili olmayan kullanicilar icin bos liste don (anonymous projection).
            return Ok(Array.Empty<object>());

        var advisees = await (
            from student in _context.Students.AsNoTracking()
            join user in _context.Users.AsNoTracking() on student.UserId equals user.Id
            join program in _context.StudentPrograms.AsNoTracking() on student.Id equals program.StudentId
            join academicProgram in _context.AcademicPrograms.AsNoTracking() on program.ProgramId equals academicProgram.Id
            where student.AdvisorId == instructorId.Value
                && student.Status == StudentStatus.Active
                && program.IsActive
                && program.Type == StudentProgramType.Main
            orderby user.LastName, user.FirstName
            select new
            {
                StudentId = student.Id,
                student.StudentNumber,
                StudentName = user.FirstName + " " + user.LastName,
                ProgramName = academicProgram.Name,
                student.CompletedSemesters,
                student.Cgpa
            })
            .ToListAsync(cancellationToken);

        // Aktif dönem plan durumları (sekme filtreleri için) — tek sorguda topla.
        var adviseeIds = advisees.Select(a => a.StudentId).ToList();
        var planStatusMap = new Dictionary<int, RegistrationPlanStatus>();
        var activeTerm = await _context.AcademicTerms.AsNoTracking().ActiveTerm().FirstOrDefaultAsync(cancellationToken);
        if (activeTerm != null && adviseeIds.Count > 0)
        {
            planStatusMap = await _context.RegistrationPlans.AsNoTracking()
                .Where(rp => adviseeIds.Contains(rp.StudentId) && rp.TermId == activeTerm.Id)
                .ToDictionaryAsync(rp => rp.StudentId, rp => rp.Status, cancellationToken);
        }

        var retentionThreshold = await _systemConfig.GetDecimalAsync("GPA.RetentionThreshold", 1.70m, cancellationToken);

        var result = advisees.Select(a => new
        {
            a.StudentId,
            a.StudentNumber,
            a.StudentName,
            a.ProgramName,
            a.CompletedSemesters,
            a.Cgpa,
            PlanStatus = planStatusMap.TryGetValue(a.StudentId, out var st) ? st.ToString() : "None",
            IsAtRisk = a.CompletedSemesters >= 1 && a.Cgpa < retentionThreshold,
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// GET api/advisor/dashboard-stats — danışman ana sayfa özet istatistikleri.
    /// Öğretim üyesi profili olmayan kullanıcı için sıfırlanmış kart döner (401 değil).
    /// </summary>
    [HttpGet("dashboard-stats")]
    [RequirePermission("menu.ders-kayit-duzenleme.view")]
    public async Task<IActionResult> GetDashboardStats(CancellationToken cancellationToken = default)
    {
        var instructorId = await GetInstructorIdAsync();
        if (instructorId == null)
            return Ok(new
            {
                adviseeCount = 0,
                pendingApprovalCount = 0,
                atRiskCount = 0,
                approvedThisTerm = 0,
                rejectedThisTerm = 0,
                retentionThreshold = 0m,
            });

        var retentionThreshold = await _systemConfig.GetDecimalAsync("GPA.RetentionThreshold", 1.70m, cancellationToken);

        var advisees = await _context.Students.AsNoTracking()
            .Where(s => s.AdvisorId == instructorId.Value && s.Status == StudentStatus.Active)
            .Select(s => new { s.Id, s.Cgpa, s.CompletedSemesters })
            .ToListAsync(cancellationToken);

        var adviseeIds = advisees.Select(a => a.Id).ToList();
        // Retention kuralı 1. dönem sonrası işler (CLAUDE.md): completedSemesters >= 1.
        var atRiskCount = advisees.Count(a => a.CompletedSemesters >= 1 && a.Cgpa < retentionThreshold);

        var pendingApprovalCount = 0;
        var approvedThisTerm = 0;
        var rejectedThisTerm = 0;

        var activeTerm = await _context.AcademicTerms.AsNoTracking().ActiveTerm().FirstOrDefaultAsync(cancellationToken);
        if (activeTerm != null && adviseeIds.Count > 0)
        {
            pendingApprovalCount = await _context.CourseRegistrations.AsNoTracking()
                .Where(cr => adviseeIds.Contains(cr.StudentId)
                    && cr.CourseSection.TermId == activeTerm.Id
                    && cr.Status == RegistrationStatus.PendingAdvisorApproval)
                .Select(cr => cr.StudentId)
                .Distinct()
                .CountAsync(cancellationToken);

            var planStatuses = await _context.RegistrationPlans.AsNoTracking()
                .Where(rp => adviseeIds.Contains(rp.StudentId) && rp.TermId == activeTerm.Id)
                .Select(rp => rp.Status)
                .ToListAsync(cancellationToken);

            approvedThisTerm = planStatuses.Count(s => s == RegistrationPlanStatus.Approved);
            rejectedThisTerm = planStatuses.Count(s => s == RegistrationPlanStatus.Rejected);
        }

        return Ok(new
        {
            adviseeCount = advisees.Count,
            pendingApprovalCount,
            atRiskCount,
            approvedThisTerm,
            rejectedThisTerm,
            retentionThreshold,
        });
    }

    /// <summary>
    /// GET api/advisor/students/{studentId}/registrations — danışmanı olunan öğrencinin
    /// mevcut dönem kayıtları (öğrenci adına düzenleme için).
    /// </summary>
    [HttpGet("students/{studentId:int}/registrations")]
    [RequirePermission("menu.ders-kayit-duzenleme.view")]
    public async Task<IActionResult> GetStudentRegistrations(int studentId)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null)
            return guard;

        var registrations = await _registrationService.GetStudentRegistrationsAsync(studentId);
        return Ok(registrations);
    }

    /// <summary>
    /// POST api/advisor/students/{studentId}/add — öğrenci adına ders ekle.
    /// İş kuralları ve faz kontrolü AddToPlanAsync içinde uygulanır (öğrenci akışıyla aynı).
    /// </summary>
    [HttpPost("students/{studentId:int}/add")]
    [RequirePermission("menu.ders-kayit-duzenleme.approve")]
    public async Task<IActionResult> AddCourseForStudent(int studentId, [FromBody] RegistrationPlanItemDto dto)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null)
            return guard;

        var result = await _registrationService.AddToPlanAsync(studentId, dto.CourseSectionId);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// DELETE api/advisor/students/{studentId}/registrations/{registrationId} — öğrenci adına ders çıkar.
    /// </summary>
    [HttpDelete("students/{studentId:int}/registrations/{registrationId:int}")]
    [RequirePermission("menu.ders-kayit-duzenleme.approve")]
    public async Task<IActionResult> DropCourseForStudent(int studentId, int registrationId)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null)
            return guard;

        var result = await _registrationService.DropCourseAsync(studentId, registrationId);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("approve")]
    [RequirePermission("menu.ders-kayit-duzenleme.approve")]
    public async Task<IActionResult> Approve([FromBody] ApprovalActionDto dto)
    {
        var instructorId = await GetInstructorIdAsync();
        if (instructorId == null)
            // Mutating action: 403 Forbid — semantik olarak dogru, axios logout etmez.
            return StatusCode(403, new { error = "Bu islem icin ogretim uyesi profili gerekli." });

        var result = await _registrationService.ApproveStudentRegistrationsAsync(instructorId.Value, dto);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("reject")]
    [RequirePermission("menu.ders-kayit-duzenleme.reject")]
    public async Task<IActionResult> Reject([FromBody] ApprovalActionDto dto)
    {
        var instructorId = await GetInstructorIdAsync();
        if (instructorId == null)
            // Mutating action: 403 Forbid — semantik olarak dogru, axios logout etmez.
            return StatusCode(403, new { error = "Bu islem icin ogretim uyesi profili gerekli." });

        var result = await _registrationService.RejectStudentRegistrationsAsync(instructorId.Value, dto);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Çağıran kullanıcının verilen öğrencinin danışmanı (advisor instructor) olduğunu doğrular.
    /// Değilse 403 döner (mutating/erişim akışları için — axios logout etmez). Uygunsa null.
    /// </summary>
    private async Task<IActionResult?> EnsureAdviseeAsync(int studentId)
    {
        var instructorId = await GetInstructorIdAsync();
        if (instructorId == null)
            return StatusCode(403, new { error = "Bu islem icin ogretim uyesi profili gerekli." });

        var isAdvisee = await _context.Students
            .AnyAsync(s => s.Id == studentId && s.AdvisorId == instructorId.Value);
        if (!isAdvisee)
            return StatusCode(403, new { error = "Bu ogrencinin danismani degilsiniz." });

        return null;
    }

    private async Task<int?> GetInstructorIdAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.UserId == userId);
        return instructor?.Id;
    }

    private static string ResolveCalculationMethodLabel(string? value) =>
        string.Equals(value, "load-based", StringComparison.OrdinalIgnoreCase)
            ? "Yük Bazlı"
            : "Standart";
}
