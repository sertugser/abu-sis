using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.Scheduling.Services;
using SIS.Application.StudentCard.DTOs;
using SIS.Application.StudentCard.Services;
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
    private readonly ICurriculumStatusService _curriculumStatus;
    private readonly IStudentTranscriptService _transcript;
    private readonly IGpaCalculationService _gpa;
    private readonly IStudentTimetableService _timetable;
    private readonly IDepartmentScheduleService _departmentSchedule;
    private readonly ITranscriptScenarioService _transcriptScenario;
    private readonly IRegistrationPlanPdfService _planPdf;

    public AdvisorController(
        ICourseRegistrationService registrationService,
        AppDbContext context,
        ISystemConfigProvider systemConfig,
        ICurriculumStatusService curriculumStatus,
        IStudentTranscriptService transcript,
        IGpaCalculationService gpa,
        IStudentTimetableService timetable,
        IDepartmentScheduleService departmentSchedule,
        ITranscriptScenarioService transcriptScenario,
        IRegistrationPlanPdfService planPdf)
    {
        _registrationService = registrationService;
        _context = context;
        _systemConfig = systemConfig;
        _curriculumStatus = curriculumStatus;
        _transcript = transcript;
        _gpa = gpa;
        _timetable = timetable;
        _departmentSchedule = departmentSchedule;
        _transcriptScenario = transcriptScenario;
        _planPdf = planPdf;
    }

    [HttpGet("pending-approvals")]
    [RequirePermission("menu.ders-kayit-onay.view")]
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
    [RequirePermission("menu.ders-kayit-onay.view")]
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
                FirstName = user.FirstName,
                LastName = user.LastName,
                ProgramName = academicProgram.Name,
                FacultyName = academicProgram.Department.Faculty.Name,
                CurriculumName = program.Curriculum.Name,
                CurriculumVersion = program.Curriculum.Version,
                program.EnrollmentDate,
                academicProgram.DegreeLevel,
                student.CompletedSemesters,
                student.Cgpa
            })
            .ToListAsync(cancellationToken);

        // Aktif dönem plan durumları + tarihleri (sekme filtreleri + durum başlığı için) — tek sorguda topla.
        var adviseeIds = advisees.Select(a => a.StudentId).ToList();
        var planMap = new Dictionary<int, (RegistrationPlanStatus Status, DateTime? SubmittedAt, DateTime? UpdatedAt)>();
        var activeTerm = await _context.AcademicTerms.AsNoTracking().ActiveTerm().FirstOrDefaultAsync(cancellationToken);
        if (activeTerm != null && adviseeIds.Count > 0)
        {
            planMap = await _context.RegistrationPlans.AsNoTracking()
                .Where(rp => adviseeIds.Contains(rp.StudentId) && rp.TermId == activeTerm.Id)
                .ToDictionaryAsync(
                    rp => rp.StudentId,
                    rp => new ValueTuple<RegistrationPlanStatus, DateTime?, DateTime?>(rp.Status, rp.SubmittedAt, rp.UpdatedAt),
                    cancellationToken);
        }

        var retentionThreshold = await _systemConfig.GetDecimalAsync("GPA.RetentionThreshold", 1.70m, cancellationToken);

        var result = advisees.Select(a =>
        {
            planMap.TryGetValue(a.StudentId, out var plan);
            var status = planMap.ContainsKey(a.StudentId) ? plan.Status.ToString() : "None";
            // Danışman onay/red anı = Approved/Rejected planda UpdatedAt.
            DateTime? decidedAt = plan.Status is RegistrationPlanStatus.Approved or RegistrationPlanStatus.Rejected
                ? plan.UpdatedAt
                : null;
            // Normal öğrenim süresi (yarıyıl) — derece türüne göre. completedSemesters bunu aşmadıysa "Normal Süresi İçinde".
            var normalDuration = a.DegreeLevel switch
            {
                DegreeLevel.Associate => 4,
                DegreeLevel.Bachelor => 8,
                DegreeLevel.Master => 4,
                DegreeLevel.Doctorate => 8,
                _ => 8
            };
            // Bağlı olunan müfredat gösterimi (Proliz): Ad varsa Ad, yoksa "Sürüm Program",
            // o da yoksa program adı (asla boş "—" kalmasın).
            var curriculumDisplay = !string.IsNullOrWhiteSpace(a.CurriculumName)
                ? a.CurriculumName!
                : !string.IsNullOrWhiteSpace(a.CurriculumVersion)
                    ? $"{a.CurriculumVersion} {a.ProgramName}".Trim()
                    : a.ProgramName;
            return new
            {
                a.StudentId,
                a.StudentNumber,
                a.StudentName,
                a.FirstName,
                a.LastName,
                a.ProgramName,
                a.FacultyName,
                CurriculumVersion = curriculumDisplay,
                EnrollmentDate = a.EnrollmentDate,
                a.CompletedSemesters,
                ClassYear = (a.CompletedSemesters / 2) + 1,
                StudyDurationStatus = a.CompletedSemesters <= normalDuration ? "Normal Süresi İçinde" : "Normal Süre Aşıldı",
                a.Cgpa,
                PlanStatus = status,
                PlanSubmittedAt = plan.SubmittedAt,
                PlanDecidedAt = decidedAt,
                IsAtRisk = a.CompletedSemesters >= 1 && a.Cgpa < retentionThreshold,
            };
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// GET api/advisor/dashboard-stats — danışman ana sayfa özet istatistikleri.
    /// Öğretim üyesi profili olmayan kullanıcı için sıfırlanmış kart döner (401 değil).
    /// </summary>
    [HttpGet("dashboard-stats")]
    [RequirePermission("menu.ders-kayit-onay.view")]
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
    [RequirePermission("menu.ders-kayit-onay.view")]
    public async Task<IActionResult> GetStudentRegistrations(int studentId)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null)
            return guard;

        var registrations = await _registrationService.GetStudentRegistrationsAsync(studentId);
        return Ok(registrations);
    }

    /// <summary>GET .../course-history — danışanın aldığı dersler / ders kayıt geçmişi (Proliz "Aldığı Dersler").</summary>
    [HttpGet("students/{studentId:int}/course-history")]
    [RequirePermission("menu.ders-kayit-onay.view")]
    public async Task<IActionResult> GetStudentCourseHistory(int studentId)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null) return guard;
        return Ok(await _registrationService.GetCourseHistoryAsync(studentId));
    }

    /// <summary>GET .../curriculum-status — danışanın müfredat durumu (Proliz "Müfredat Durumu").</summary>
    [HttpGet("students/{studentId:int}/curriculum-status")]
    [RequirePermission("menu.ders-kayit-onay.view")]
    public async Task<IActionResult> GetStudentCurriculumStatus(int studentId, CancellationToken ct)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null) return guard;
        var status = await _curriculumStatus.GetCurriculumStatusAsync(studentId, ct);
        if (status == null)
            return NotFound(new { error = "Müfredat durumu bulunamadı." });
        return Ok(status);
    }

    /// <summary>GET .../transcript — danışanın transkripti (Proliz "Transkript").</summary>
    [HttpGet("students/{studentId:int}/transcript")]
    [RequirePermission("menu.ders-kayit-onay.view")]
    public async Task<IActionResult> GetStudentTranscript(int studentId, CancellationToken ct)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null) return guard;
        var transcript = await _transcript.GetTranscriptAsync(studentId, ct);
        if (transcript == null)
            return NotFound(new { error = "Transkript bulunamadı." });
        return Ok(transcript);
    }

    /// <summary>GET .../term-gpa — danışanın dönem ortalamaları geçmişi (Proliz "Dönem Ortalamaları").</summary>
    [HttpGet("students/{studentId:int}/term-gpa")]
    [RequirePermission("menu.ders-kayit-onay.view")]
    public async Task<IActionResult> GetStudentTermGpa(int studentId)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null) return guard;
        return Ok(await _gpa.GetTermGpaHistoryAsync(studentId));
    }

    /// <summary>
    /// POST api/advisor/students/{studentId}/add — öğrenci adına ders ekle.
    /// İş kuralları ve faz kontrolü AddToPlanAsync içinde uygulanır (öğrenci akışıyla aynı).
    /// Proliz modeli: kayıt doğrudan PendingAdvisorApproval oluşturulur → "Danışman Onay"
    /// akışı (ApproveStudentRegistrationsAsync) eklenen dersi de Active'e çevirir.
    /// </summary>
    [HttpPost("students/{studentId:int}/add")]
    [RequirePermission("menu.ders-kayit-onay.approve")]
    public async Task<IActionResult> AddCourseForStudent(int studentId, [FromBody] RegistrationPlanItemDto dto)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null)
            return guard;

        var result = await _registrationService.AddToPlanAsync(
            studentId, dto.CourseSectionId, RegistrationStatus.PendingAdvisorApproval);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// DELETE api/advisor/students/{studentId}/registrations/{registrationId} — öğrenci adına ders çıkar.
    /// </summary>
    [HttpDelete("students/{studentId:int}/registrations/{registrationId:int}")]
    [RequirePermission("menu.ders-kayit-onay.approve")]
    public async Task<IActionResult> DropCourseForStudent(int studentId, int registrationId)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null)
            return guard;

        var result = await _registrationService.DropCourseAsync(studentId, registrationId, asAdvisor: true);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("approve")]
    [RequirePermission("menu.ders-kayit-onay.approve")]
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
    [RequirePermission("menu.ders-kayit-onay.reject")]
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

    /// <summary>GET .../timetable/terms — advisee'nin kayıtlı olduğu dönemler.</summary>
    [HttpGet("students/{studentId:int}/timetable/terms")]
    [RequirePermission("menu.ders-kayit-onay.view")]
    public async Task<IActionResult> GetStudentTimetableTerms(int studentId, CancellationToken ct)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null) return guard;
        return Ok(await _timetable.GetEnrolledTermsAsync(studentId, ct));
    }

    /// <summary>GET .../timetable?termId= — advisee haftalık ders programı.</summary>
    [HttpGet("students/{studentId:int}/timetable")]
    [RequirePermission("menu.ders-kayit-onay.view")]
    public async Task<IActionResult> GetStudentTimetable(int studentId, [FromQuery] int? termId, CancellationToken ct)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null) return guard;
        return Ok(await _timetable.GetTimetableAsync(studentId, termId, ct));
    }

    /// <summary>GET .../department-schedule/context — advisee bölüm ders programı dropdown/default'ları.</summary>
    [HttpGet("students/{studentId:int}/department-schedule/context")]
    [RequirePermission("menu.ders-kayit-onay.view")]
    public async Task<IActionResult> GetStudentDeptScheduleContext(int studentId, CancellationToken ct)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null) return guard;
        try { return Ok(await _departmentSchedule.GetContextAsync(studentId, ct)); }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>GET .../department-schedule?termId=&programId=&classYear= — advisee bölüm haftalık programı.</summary>
    [HttpGet("students/{studentId:int}/department-schedule")]
    [RequirePermission("menu.ders-kayit-onay.view")]
    public async Task<IActionResult> GetStudentDeptSchedule(
        int studentId, [FromQuery] int termId, [FromQuery] int programId, [FromQuery] int classYear, CancellationToken ct)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null) return guard;
        if (termId <= 0 || programId <= 0)
            return BadRequest(new { error = "Geçerli termId ve programId gönderilmelidir." });
        try { return Ok(await _departmentSchedule.GetScheduleAsync(studentId, termId, programId, classYear, ct)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>POST .../transcript-scenario — advisee what-if hesabı (saf; guard sahiplik için).</summary>
    [HttpPost("students/{studentId:int}/transcript-scenario")]
    [RequirePermission("menu.ders-kayit-onay.view")]
    public async Task<IActionResult> CalculateStudentScenario(int studentId, [FromBody] TranscriptScenarioRequestDto request)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null) return guard;
        return Ok(_transcriptScenario.Calculate(request));
    }

    /// <summary>GET .../registration-plan/pdf — advisee kayıt planı PDF.</summary>
    [HttpGet("students/{studentId:int}/registration-plan/pdf")]
    [RequirePermission("menu.ders-kayit-onay.view")]
    public async Task<IActionResult> GetStudentRegistrationPlanPdf(int studentId, CancellationToken ct)
    {
        var guard = await EnsureAdviseeAsync(studentId);
        if (guard != null) return guard;
        var bytes = await _planPdf.GenerateAsync(studentId, ct);
        return File(bytes, "application/pdf", $"ders-kayit-plani-{studentId}.pdf");
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
