using System.Security.Claims;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InstructorController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ISchedulingService _schedulingService;
    private readonly IWebModuleControlService _webModuleControls;
    private readonly ISystemConfigProvider _systemConfig;

    public InstructorController(
        AppDbContext context,
        ISchedulingService schedulingService,
        ISystemConfigProvider systemConfig,
        IWebModuleSettingsProvider? webSettings = null,
        IWebModuleControlService? webModuleControls = null)
    {
        _context = context;
        _schedulingService = schedulingService;
        _systemConfig = systemConfig;
        _webModuleControls = webModuleControls ?? new NullWebModuleControlService();
    }

    public sealed record UpdateInstructorProfileDto(
        string Email,
        int DepartmentId,
        string? OfficeHours,
        bool AdvisorOptOutRequested);

    public sealed record CreateAcademicCvActivityDto(
        int ActivityYear,
        string Title,
        string? Description);

    public sealed record UpdateAcademicCvActivityDto(
        int ActivityYear,
        string Title,
        string? Description);

    /// <summary>
    /// GET api/instructor/profile — get the authenticated instructor's profile.
    /// </summary>
    [HttpGet("profile")]
    [RequirePermission("menu.derslerim.view")]
    public async Task<IActionResult> GetProfile()
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var user = await _context.Users.FindAsync(instructor.UserId);
        var department = await _context.Departments
            .Include(d => d.Faculty)
            .FirstOrDefaultAsync(d => d.Id == instructor.DepartmentId);
        var simplifyDashboardStatistics = await _systemConfig.GetBoolAsync("Instructor.SimplifyDashboardStatistics", false);
        var faqUrl = await _systemConfig.GetStringAsync("Instructor.FaqUrl", string.Empty);
        var requireOfficeHours = await _systemConfig.GetBoolAsync("Instructor.RequireOfficeHoursOnProfileUpdate", false);
        var primaryEmailImmutable = await _systemConfig.GetBoolAsync("Instructor.PrimaryEmailImmutable", false);
        var enableAdvisorOptOutOptions = await _systemConfig.GetBoolAsync("Instructor.EnableAdvisorOptOutOptions", true);
        var deadlineText = await _systemConfig.GetStringAsync("Instructor.ProfileOrgUnitUpdateDeadline", string.Empty);
        var canUpdateDepartment = CanUpdateDepartment(deadlineText);
        var weeklyHoursPolicy = await InstructorWeeklyHoursPolicy.LoadAsync(_systemConfig);

        return Ok(new
        {
            InstructorId = instructor.Id,
            Title = instructor.Title,
            FirstName = user?.FirstName ?? "",
            LastName = user?.LastName ?? "",
            Email = user?.Email ?? "",
            DepartmentName = department?.Name ?? "",
            FacultyName = department?.Faculty?.Name ?? "",
            MaxWeeklyHours = weeklyHoursPolicy.Resolve(instructor.Title, instructor.MaxWeeklyHours),
            DepartmentId = instructor.DepartmentId,
            OfficeHours = instructor.OfficeHours,
            AdvisorOptOutRequested = instructor.AdvisorOptOutRequested,
            RequireOfficeHoursOnProfileUpdate = requireOfficeHours,
            PrimaryEmailImmutable = primaryEmailImmutable,
            EnableAdvisorOptOutOptions = enableAdvisorOptOutOptions,
            ProfileOrgUnitUpdateDeadline = deadlineText,
            CanUpdateDepartment = canUpdateDepartment,
            SimplifyDashboardStatistics = simplifyDashboardStatistics,
            FaqUrl = faqUrl,
            AcademicCvActiveYear = await _systemConfig.GetIntAsync("Instructor.AcademicCvActiveYear", DateTime.UtcNow.Year),
            AcademicCvApprovedActivityEditable = await _systemConfig.GetBoolAsync("Instructor.AcademicCvApprovedActivityEditable", false),
            AcademicCvAutoApprove = await _systemConfig.GetBoolAsync("Instructor.AcademicCvAutoApprove", false)
        });
    }

    [HttpPut("profile")]
    [RequirePermission("menu.derslerim.view")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateInstructorProfileDto dto, CancellationToken cancellationToken)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == instructor.UserId, cancellationToken);
        if (user == null)
            return NotFound(new { error = "Kullanici kaydi bulunamadi." });

        var requireOfficeHours = await _systemConfig.GetBoolAsync("Instructor.RequireOfficeHoursOnProfileUpdate", false, cancellationToken);
        var primaryEmailImmutable = await _systemConfig.GetBoolAsync("Instructor.PrimaryEmailImmutable", false, cancellationToken);
        var enableAdvisorOptOutOptions = await _systemConfig.GetBoolAsync("Instructor.EnableAdvisorOptOutOptions", true, cancellationToken);
        var deadlineText = await _systemConfig.GetStringAsync("Instructor.ProfileOrgUnitUpdateDeadline", string.Empty, cancellationToken);

        var normalizedEmail = dto.Email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            return BadRequest(new { error = "E-posta bos birakilamaz." });

        if (requireOfficeHours && string.IsNullOrWhiteSpace(dto.OfficeHours))
            return BadRequest(new { error = "Sistem ayarina gore ofis saati tanimi zorunludur." });

        var emailChanged = !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase);
        if (primaryEmailImmutable && emailChanged)
            return BadRequest(new { error = "Birincil e-posta adresi sistem ayari nedeniyle degistirilemez." });

        if (dto.DepartmentId != instructor.DepartmentId && !CanUpdateDepartment(deadlineText))
            return BadRequest(new { error = $"Birim/Bolum guncelleme suresi doldu. Son tarih: {deadlineText}" });

        if (!enableAdvisorOptOutOptions && dto.AdvisorOptOutRequested != instructor.AdvisorOptOutRequested)
            return BadRequest(new { error = "Danismanlik tercih degisikligi su anda sistem ayarinda kapali." });

        var departmentExists = await _context.Departments
            .AsNoTracking()
            .AnyAsync(d => d.Id == dto.DepartmentId, cancellationToken);
        if (!departmentExists)
            return BadRequest(new { error = "Secilen bolum bulunamadi." });

        if (emailChanged)
        {
            var lookupEmail = normalizedEmail.ToUpperInvariant();
            var emailInUse = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id != user.Id && u.NormalizedEmail == lookupEmail, cancellationToken);
            if (emailInUse)
                return BadRequest(new { error = "Bu e-posta adresi baska bir kullanici tarafindan kullaniliyor." });

            user.Email = normalizedEmail;
            user.UserName = normalizedEmail;
            user.NormalizedEmail = lookupEmail;
            user.NormalizedUserName = lookupEmail;
        }

        instructor.DepartmentId = dto.DepartmentId;
        instructor.OfficeHours = dto.OfficeHours?.Trim() ?? string.Empty;
        instructor.AdvisorOptOutRequested = dto.AdvisorOptOutRequested;
        instructor.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetProfile();
    }

    [HttpGet("academic-cv")]
    [RequirePermission("menu.derslerim.view")]
    public async Task<IActionResult> GetAcademicCv(CancellationToken cancellationToken)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var activeYear = await _systemConfig.GetIntAsync("Instructor.AcademicCvActiveYear", DateTime.UtcNow.Year, cancellationToken);
        var approvedEditable = await _systemConfig.GetBoolAsync("Instructor.AcademicCvApprovedActivityEditable", false, cancellationToken);
        var autoApprove = await _systemConfig.GetBoolAsync("Instructor.AcademicCvAutoApprove", false, cancellationToken);

        var activities = await _context.AcademicCvActivities
            .AsNoTracking()
            .Where(a => a.InstructorId == instructor.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.ActivityYear,
                a.Title,
                a.Description,
                Status = a.Status.ToString(),
                a.CreatedAt,
                a.UpdatedAt,
                a.ApprovedAt,
                CanEdit = a.Status != ApprovalStepStatus.Approved || approvedEditable
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            ActiveYear = activeYear,
            ApprovedActivityEditable = approvedEditable,
            AutoApprove = autoApprove,
            Activities = activities
        });
    }

    [HttpPost("academic-cv")]
    [RequirePermission("menu.derslerim.view")]
    public async Task<IActionResult> CreateAcademicCvActivity([FromBody] CreateAcademicCvActivityDto dto, CancellationToken cancellationToken)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var validationError = await ValidateAcademicCvPayload(dto.ActivityYear, dto.Title, cancellationToken);
        if (validationError != null)
            return BadRequest(new { error = validationError });

        var autoApprove = await _systemConfig.GetBoolAsync("Instructor.AcademicCvAutoApprove", false, cancellationToken);
        var activity = new AcademicCvActivity
        {
            InstructorId = instructor.Id,
            ActivityYear = dto.ActivityYear,
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            Status = autoApprove ? ApprovalStepStatus.Approved : ApprovalStepStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = autoApprove ? DateTime.UtcNow : null
        };

        _context.AcademicCvActivities.Add(activity);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetAcademicCv(cancellationToken);
    }

    [HttpPut("academic-cv/{id:int}")]
    [RequirePermission("menu.derslerim.view")]
    public async Task<IActionResult> UpdateAcademicCvActivity(int id, [FromBody] UpdateAcademicCvActivityDto dto, CancellationToken cancellationToken)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var validationError = await ValidateAcademicCvPayload(dto.ActivityYear, dto.Title, cancellationToken);
        if (validationError != null)
            return BadRequest(new { error = validationError });

        var activity = await _context.AcademicCvActivities
            .FirstOrDefaultAsync(a => a.Id == id && a.InstructorId == instructor.Id, cancellationToken);
        if (activity == null)
            return NotFound(new { error = "Faaliyet kaydi bulunamadi." });

        var approvedEditable = await _systemConfig.GetBoolAsync("Instructor.AcademicCvApprovedActivityEditable", false, cancellationToken);
        if (activity.Status == ApprovalStepStatus.Approved && !approvedEditable)
            return BadRequest(new { error = "Onayli faaliyet kaydi sistem ayari nedeniyle duzenlenemez." });

        activity.ActivityYear = dto.ActivityYear;
        activity.Title = dto.Title.Trim();
        activity.Description = dto.Description?.Trim() ?? string.Empty;
        activity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetAcademicCv(cancellationToken);
    }

    /// <summary>
    /// GET api/instructor/my-sections — sections the instructor teaches.
    /// Opsiyonel termId parametresi verilmezse aktif dönem kullanılır.
    /// Geçmiş dönem section'larına erişim (değerlendirme sonuçları için salt-okunur).
    /// </summary>
    [HttpGet("my-sections")]
    [RequirePermission("menu.derslerim.view")]
    public async Task<IActionResult> GetMySections([FromQuery] int? termId = null)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var nowInstr = DateTime.UtcNow;
        var term = termId.HasValue
            ? await _context.AcademicTerms.FirstOrDefaultAsync(t => t.Id == termId.Value)
            : await _context.AcademicTerms.FirstOrDefaultAsync(t => !t.IsCancelled && t.TermStart <= nowInstr && t.TermEnd >= nowInstr);

        if (term == null)
            return NotFound(new { error = termId.HasValue ? "Donem bulunamadi." : "Aktif donem bulunamadi." });

        var sections = await _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Classroom)
            .Include(cs => cs.Registrations)
            .Where(cs => cs.InstructorId == instructor.Id && cs.TermId == term.Id)
            .ToListAsync();

        // Load SectionSchedule records for schedule display
        var mySectionIds = sections.Select(s => s.Id).ToList();
        var mySectionSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => mySectionIds.Contains(ss.CourseSectionId))
            .ToListAsync();
        var myScheduleMap = mySectionSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => ScheduleHelper.FormatSchedule(g));
        // 2026-05-10 — Plan E: structured schedule blocks (replaces frontend string parsing).
        // BuildScheduleBlocks BlockType-aware merge yapar, frontend `parseScheduleSummary`
        // helper'ı bu sayede silindi.
        var myScheduleBlockMap = mySectionSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => ScheduleHelper.BuildScheduleBlocks(g));

        var result = sections.Select(s => new
        {
            SectionId = s.Id,
            SectionNumber = s.SectionNumber,
            CourseCode = s.Course.Code,
            CourseName = s.Course.Name,
            Ects = s.Course.Ects,
            TheoryHours = s.Course.TheoryHours,
            LabHours = s.Course.LabHours,
            Capacity = s.Capacity,
            EnrolledCount = s.EnrolledCount,
            AvailableSeats = Math.Max(0, s.Capacity - s.EnrolledCount),
            ClassroomName = s.Classroom?.Name ?? "",
            Schedule = myScheduleMap.GetValueOrDefault(s.Id, string.Empty),
            Schedules = myScheduleBlockMap.GetValueOrDefault(s.Id, new()),
            IsActive = s.IsActive,
            IsFinalized = s.IsFinalized,
            ActiveStudents = s.Registrations.Count(r => r.Status == Domain.Enums.RegistrationStatus.Active)
        })
        .OrderBy(s => s.CourseCode)
        .ToList();

        return Ok(new
        {
            Term = term.Name,
            TermId = term.Id,
            TotalSections = result.Count,
            TotalStudents = result.Sum(s => s.ActiveStudents),
            Sections = result
        });
    }

    /// <summary>
    /// GET api/instructor/my-terms — instructor'ın section'ı olan dönemlerin listesi.
    /// Değerlendirme sonuçları sayfasında geçmiş dönem seçimi için kullanılır.
    /// </summary>
    [HttpGet("my-terms")]
    [RequirePermission("lookup.term")]
    public async Task<IActionResult> GetMyTerms()
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var terms = await _context.CourseSections
            .Where(cs => cs.InstructorId == instructor.Id)
            .Select(cs => cs.Term)
            .Distinct()
            .OrderByDescending(t => t.Year)
            .ThenByDescending(t => t.TermType)
            .Select(t => new
            {
                Id = t.Id,
                Name = t.Name,
                Year = t.Year,
                Semester = t.TermType,
                IsActive = !t.IsCancelled && t.TermStart <= DateTime.UtcNow && t.TermEnd >= DateTime.UtcNow
            })
            .ToListAsync();

        return Ok(terms);
    }

    /// <summary>
    /// GET api/instructor/section/{id}/students — list students enrolled in a specific section.
    /// </summary>
    [HttpGet("section/{id}/students")]
    [RequirePermission("menu.derslerim.view")]
    public async Task<IActionResult> GetSectionStudents(int id)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == id && cs.InstructorId == instructor.Id);

        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.Student)
            .Where(cr => cr.CourseSectionId == id
                && cr.Status != Domain.Enums.RegistrationStatus.Dropped
                && cr.Status != Domain.Enums.RegistrationStatus.Draft)
            .ToListAsync();

        var studentUserIds = registrations.Select(r => r.Student.UserId).ToList();
        var userMap = await _context.Users
            .Where(u => studentUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);
        var contactInfoVisible = await _systemConfig.GetBoolAsync("Instructor.StudentContactInfoVisible", true);
        var phoneVisibility = await _webModuleControls.EvaluateAsync("instructor-student-phone");
        var showStudentPhone = contactInfoVisible && phoneVisibility.IsOpen;

        var students = registrations.Select(r =>
        {
            var user = userMap.GetValueOrDefault(r.Student.UserId);
            return new
            {
                StudentId = r.StudentId,
                StudentNumber = r.Student.StudentNumber,
                FirstName = user?.FirstName ?? "",
                LastName = user?.LastName ?? "",
                Email = contactInfoVisible ? user?.Email ?? "" : "",
                PhoneNumber = showStudentPhone ? user?.PhoneNumber ?? "" : "",
                Status = r.Status.ToString(),
                Grade = r.Grade?.ToString()
            };
        })
        .OrderBy(s => s.LastName)
        .ThenBy(s => s.FirstName)
        .ToList();

        return Ok(new
        {
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            SectionId = section.Id,
            Students = students
        });
    }

    /// <summary>
    /// POST api/instructor/section/{id}/schedule/apply — apply a schedule suggestion to the instructor's section.
    /// </summary>
    [HttpPost("section/{id}/schedule/apply")]
    [RequirePermission("menu.ders-programim.edit")]
    public async Task<IActionResult> ApplySectionSchedule(
        int id,
        [FromBody] ApplyScheduleRequestDto request,
        CancellationToken cancellationToken)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        if (!await InstructorOwnsSectionAsync(instructor.Id, id, cancellationToken))
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        request.SectionId = id;

        var result = await _schedulingService.ApplyAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST api/instructor/section/{id}/schedule/validate — validate the current instructor section schedule.
    /// </summary>
    [HttpPost("section/{id}/schedule/validate")]
    [RequirePermission("menu.ders-programim.view")]
    public async Task<IActionResult> ValidateSectionSchedule(
        int id,
        [FromBody] ScheduleValidationRequestDto? request,
        CancellationToken cancellationToken)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        if (!await InstructorOwnsSectionAsync(instructor.Id, id, cancellationToken))
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        var payload = request ?? new ScheduleValidationRequestDto();
        payload.SectionId = id;
        payload.InstructorId = instructor.Id;

        var result = await _schedulingService.ValidateAsync(payload, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST api/instructor/section/{id}/schedule/suggest — generate a schedule suggestion for the current instructor section.
    /// </summary>
    [HttpPost("section/{id}/schedule/suggest")]
    [RequirePermission("menu.ders-programim.view")]
    public async Task<IActionResult> SuggestSectionSchedule(
        int id,
        [FromBody] ScheduleSuggestionRequestDto? request,
        CancellationToken cancellationToken)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        if (!await InstructorOwnsSectionAsync(instructor.Id, id, cancellationToken))
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        var payload = request ?? new ScheduleSuggestionRequestDto();
        payload.SectionId = id;
        payload.InstructorId = instructor.Id;

        var result = await _schedulingService.SuggestAsync(payload, cancellationToken);
        return Ok(result);
    }

    private async Task<Instructor?> GetInstructorAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        return await _context.Instructors.FirstOrDefaultAsync(i => i.UserId == userId);
    }

    private Task<bool> InstructorOwnsSectionAsync(int instructorId, int sectionId, CancellationToken cancellationToken) =>
        _context.CourseSections
            .AsNoTracking()
            .AnyAsync(section => section.Id == sectionId && section.InstructorId == instructorId, cancellationToken);

    private async Task<string?> ValidateAcademicCvPayload(int activityYear, string title, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "Faaliyet basligi bos birakilamaz.";

        var activeYear = await _systemConfig.GetIntAsync("Instructor.AcademicCvActiveYear", DateTime.UtcNow.Year, cancellationToken);
        if (activityYear != activeYear)
            return $"Akademik CV kayitlari yalnizca aktif yil ({activeYear}) icin girilebilir.";

        return null;
    }

    private static bool CanUpdateDepartment(string deadlineText)
    {
        if (string.IsNullOrWhiteSpace(deadlineText))
            return true;

        if (!DateOnly.TryParseExact(deadlineText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var deadline))
            return true;

        return DateOnly.FromDateTime(DateTime.UtcNow) <= deadline;
    }

}
