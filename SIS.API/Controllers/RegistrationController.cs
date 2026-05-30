using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.DTOs;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.CourseRegistration.Validators;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/registrations")]
[Authorize]
public class RegistrationController : ControllerBase
{
    private readonly ICourseRegistrationService _registrationService;
    private readonly ICourseHistoryPdfService _courseHistoryPdfService;
    private readonly IRegistrationPlanPdfService _planPdfService;
    private readonly IRegistrationValidationService _validationService;
    private readonly AppDbContext _context;

    public RegistrationController(
        ICourseRegistrationService registrationService,
        ICourseHistoryPdfService courseHistoryPdfService,
        IRegistrationPlanPdfService planPdfService,
        IRegistrationValidationService validationService,
        AppDbContext context)
    {
        _registrationService = registrationService;
        _courseHistoryPdfService = courseHistoryPdfService;
        _planPdfService = planPdfService;
        _validationService = validationService;
        _context = context;
    }

    /// <summary>
    /// POST api/registration/plan — add a course section to the student's plan.
    /// </summary>
    [HttpPost("plan")]
    [RequirePermission("menu.ders-kayit.plan")]
    public async Task<IActionResult> AddToPlan([FromBody] RegistrationPlanItemDto dto)
    {
        var planValidation = new RegistrationPlanItemDtoValidator().Validate(dto);
        if (!planValidation.IsValid)
            return BadRequest(new ApiErrorResponse(planValidation.Errors[0].ErrorMessage, "ValidationError"));

        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));

        var result = await _registrationService.AddToPlanAsync(studentId.Value, dto.CourseSectionId);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// POST api/registration/ensure-failed-courses — başarısız (alttan) dersleri uygun şubeyle
    /// öğrencinin planına otomatik ekler. Ders kayıt ekranı açılırken çağrılır. Idempotent.
    /// </summary>
    [HttpPost("ensure-failed-courses")]
    [RequirePermission("menu.ders-kayit.plan")]
    public async Task<IActionResult> EnsureFailedCourses()
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));

        var result = await _registrationService.EnsureFailedCoursesInPlanAsync(studentId.Value);
        return Ok(result);
    }

    /// <summary>
    /// POST api/registration/submit — submit the registration (validate and finalize).
    /// </summary>
    [HttpPost("submit")]
    [RequirePermission("menu.ders-kayit.submit")]
    public async Task<IActionResult> SubmitRegistration([FromBody] RegistrationSubmitDto dto)
    {
        var submitValidation = new RegistrationSubmitDtoValidator().Validate(dto);
        if (!submitValidation.IsValid)
            return BadRequest(new ApiErrorResponse(submitValidation.Errors[0].ErrorMessage, "ValidationError"));

        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));

        var result = await _registrationService.SubmitRegistrationAsync(studentId.Value, dto);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>POST api/registrations/validate — read-only "Kontrol Et"; doğrular, mutasyon yapmaz.</summary>
    [HttpPost("validate")]
    [RequirePermission("menu.ders-kayit.plan")]
    public async Task<IActionResult> Validate([FromBody] RegistrationSubmitDto dto)
    {
        var validateDtoResult = new RegistrationSubmitDtoValidator().Validate(dto);
        if (!validateDtoResult.IsValid)
            return BadRequest(new ApiErrorResponse(validateDtoResult.Errors[0].ErrorMessage, "ValidationError"));

        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));
        var result = await _validationService.ValidateRegistrationAsync(studentId.Value, dto.CourseSectionIds);
        return Ok(result); // always 200; IsValid + Errors/Warnings in body; no mutation
    }

    /// <summary>
    /// GET api/registration/my — list the student's registrations for the current term.
    /// </summary>
    [HttpGet("my")]
    [RequirePermission("menu.alinan-dersler.view")]
    public async Task<IActionResult> GetMyRegistrations()
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));

        var registrations = await _registrationService.GetStudentRegistrationsAsync(studentId.Value);
        return Ok(registrations);
    }

    /// <summary>
    /// GET api/registrations/attendance-status — list the student's active-term attendance status.
    /// </summary>
    [HttpGet("attendance-status")]
    [RequirePermission("menu.devamsizlik-durumu.view")]
    public async Task<IActionResult> GetAttendanceStatus([FromQuery] int? termId)
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));

        var status = await _registrationService.GetStudentAttendanceStatusAsync(studentId.Value, termId);
        if (status == null)
            return NotFound(new ApiErrorResponse("Dönem veya ders kaydı bulunamadı.", "AttendanceStatusNotFound"));

        return Ok(status);
    }

    /// <summary>
    /// GET api/registrations/history — list the student's course history with grades, grouped by term.
    /// </summary>
    [HttpGet("history")]
    // Paylaşılan uç: "Alınan Dersler" (MyCoursesPage) ve "Not Listesi" (GradeListPage)
    // sayfalarının ortak veri kaynağı — her iki sayfanın izninden biri yeterlidir (any-of).
    [RequirePermission("menu.alinan-dersler.view", "menu.not-listesi.view")]
    public async Task<IActionResult> GetCourseHistory()
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));

        var history = await _registrationService.GetCourseHistoryAsync(studentId.Value);
        return Ok(history);
    }

    /// <summary>GET api/registrations/plan/pdf — "Seçilen Dersler" / kayıt planı PDF raporu.</summary>
    [HttpGet("plan/pdf")]
    [RequirePermission("menu.ders-kayit.view")]
    public async Task<IActionResult> GetRegistrationPlanPdf(CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));
        var pdfBytes = await _planPdfService.GenerateAsync(studentId.Value, ct);
        var fileName = $"ders-kayit-plani-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    /// <summary>
    /// GET api/registrations/history/pdf — "Alınan Dersler" resmî PDF raporu.
    /// termId verilirse yalnız o dönem basılır; verilmezse tüm geçmiş.
    /// </summary>
    [HttpGet("history/pdf")]
    [RequirePermission("menu.alinan-dersler.view")]
    public async Task<IActionResult> GetCourseHistoryPdf([FromQuery] int? termId, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));

        var pdfBytes = await _courseHistoryPdfService.GenerateAsync(studentId.Value, termId, ct);
        var fileName = $"alinan-dersler-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    /// <summary>
    /// DELETE api/registration/{id}/drop — drop a course (add-drop period).
    /// </summary>
    [HttpDelete("{id}/drop")]
    [RequirePermission("menu.ders-kayit.drop")]
    public async Task<IActionResult> DropCourse(int id)
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));

        var result = await _registrationService.DropCourseAsync(studentId.Value, id);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// POST api/registration/{id}/withdraw — withdraw from a course.
    /// </summary>
    [HttpPost("{id}/withdraw")]
    [RequirePermission("menu.ders-kayit.withdraw")]
    public async Task<IActionResult> WithdrawCourse(int id)
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));

        var result = await _registrationService.WithdrawCourseAsync(studentId.Value, id);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Gets the Student.Id from the JWT claims (ClaimTypes.NameIdentifier -> UserId -> Student).
    /// </summary>
    private async Task<int?> GetStudentIdAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
        return student?.Id;
    }
}
