using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
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
    private readonly IWebModuleSettingsProvider _webSettings;

    public InstructorController(
        AppDbContext context,
        ISchedulingService schedulingService,
        IWebModuleSettingsProvider? webSettings = null)
    {
        _context = context;
        _schedulingService = schedulingService;
        _webSettings = webSettings ?? new NullWebModuleSettingsProvider();
    }

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

        return Ok(new
        {
            InstructorId = instructor.Id,
            Title = instructor.Title,
            FirstName = user?.FirstName ?? "",
            LastName = user?.LastName ?? "",
            Email = user?.Email ?? "",
            DepartmentName = department?.Name ?? "",
            FacultyName = department?.Faculty?.Name ?? "",
            MaxWeeklyHours = instructor.MaxWeeklyHours
        });
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
    [RequirePermission("menu.sinif-listeleri.view")]
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
        var showStudentPhone = await _webSettings.GetBoolAsync("InstructorPanel", "ShowStudentPhone", false);

        var students = registrations.Select(r =>
        {
            var user = userMap.GetValueOrDefault(r.Student.UserId);
            return new
            {
                StudentId = r.StudentId,
                StudentNumber = r.Student.StudentNumber,
                FirstName = user?.FirstName ?? "",
                LastName = user?.LastName ?? "",
                Email = user?.Email ?? "",
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

}
