using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Instructor")]
public class InstructorController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ISchedulingService _schedulingService;

    public InstructorController(AppDbContext context, ISchedulingService schedulingService)
    {
        _context = context;
        _schedulingService = schedulingService;
    }

    /// <summary>
    /// GET api/instructor/profile — get the authenticated instructor's profile.
    /// </summary>
    [HttpGet("profile")]
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
    /// GET api/instructor/my-sections — sections the instructor teaches in the active term.
    /// </summary>
    [HttpGet("my-sections")]
    public async Task<IActionResult> GetMySections()
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var activeTerm = await _context.AcademicTerms.FirstOrDefaultAsync(t => t.IsActive);
        if (activeTerm == null)
            return NotFound(new { error = "Aktif donem bulunamadi." });

        var sections = await _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Classroom)
            .Include(cs => cs.Registrations)
            .Where(cs => cs.InstructorId == instructor.Id && cs.TermId == activeTerm.Id)
            .ToListAsync();

        var result = sections.Select(s => new
        {
            SectionId = s.Id,
            CourseCode = s.Course.Code,
            CourseName = s.Course.Name,
            Ects = s.Course.Ects,
            TheoryHours = s.Course.TheoryHours,
            LabHours = s.Course.LabHours,
            Capacity = s.Capacity,
            EnrolledCount = s.EnrolledCount,
            AvailableSeats = Math.Max(0, s.Capacity - s.EnrolledCount),
            ClassroomName = s.Classroom?.Name ?? "",
            Schedule = FormatSchedule(s.ScheduleJson),
            IsActive = s.IsActive,
            ActiveStudents = s.Registrations.Count(r => r.Status == Domain.Enums.RegistrationStatus.Active)
        })
        .OrderBy(s => s.CourseCode)
        .ToList();

        return Ok(new
        {
            Term = activeTerm.Name,
            TotalSections = result.Count,
            TotalStudents = result.Sum(s => s.ActiveStudents),
            Sections = result
        });
    }

    /// <summary>
    /// GET api/instructor/section/{id}/students — list students enrolled in a specific section.
    /// </summary>
    [HttpGet("section/{id}/students")]
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
    /// GET api/instructor/availability — list the authenticated instructor's availability rules.
    /// </summary>
    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability(CancellationToken cancellationToken)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var availabilities = await _context.InstructorAvailabilities
            .AsNoTracking()
            .Where(availability => availability.InstructorId == instructor.Id)
            .OrderByDescending(availability => availability.IsPreferred)
            .ThenBy(availability => availability.DayOfWeek)
            .ThenBy(availability => availability.StartTime)
            .Select(availability => MapAvailability(availability))
            .ToListAsync(cancellationToken);

        return Ok(availabilities);
    }

    /// <summary>
    /// POST api/instructor/availability — create a new availability rule for the authenticated instructor.
    /// </summary>
    [HttpPost("availability")]
    public async Task<IActionResult> CreateAvailability(
        [FromBody] UpsertInstructorAvailabilityDto request,
        CancellationToken cancellationToken)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var validationError = ValidateAvailabilityRequest(request);
        if (validationError != null)
            return BadRequest(new { error = validationError });

        var duplicateExists = await AvailabilityRuleExistsAsync(instructor.Id, request, null, cancellationToken);
        if (duplicateExists)
            return BadRequest(new { error = "Ayni gun ve saat araligi icin bu availability kaydi zaten bulunuyor." });

        var entity = new InstructorAvailability
        {
            InstructorId = instructor.Id,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsAvailable = request.IsAvailable,
            IsPreferred = request.IsAvailable && request.IsPreferred
        };

        _context.InstructorAvailabilities.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(MapAvailability(entity));
    }

    /// <summary>
    /// PUT api/instructor/availability/{id} — update an availability rule for the authenticated instructor.
    /// </summary>
    [HttpPut("availability/{id}")]
    public async Task<IActionResult> UpdateAvailability(
        int id,
        [FromBody] UpsertInstructorAvailabilityDto request,
        CancellationToken cancellationToken)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var validationError = ValidateAvailabilityRequest(request);
        if (validationError != null)
            return BadRequest(new { error = validationError });

        var entity = await _context.InstructorAvailabilities
            .FirstOrDefaultAsync(availability => availability.Id == id && availability.InstructorId == instructor.Id, cancellationToken);

        if (entity == null)
            return NotFound(new { error = "Availability kaydi bulunamadi." });

        var duplicateExists = await AvailabilityRuleExistsAsync(instructor.Id, request, id, cancellationToken);
        if (duplicateExists)
            return BadRequest(new { error = "Ayni gun ve saat araligi icin bu availability kaydi zaten bulunuyor." });

        entity.DayOfWeek = request.DayOfWeek;
        entity.StartTime = request.StartTime;
        entity.EndTime = request.EndTime;
        entity.IsAvailable = request.IsAvailable;
        entity.IsPreferred = request.IsAvailable && request.IsPreferred;

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(MapAvailability(entity));
    }

    /// <summary>
    /// DELETE api/instructor/availability/{id} — delete an availability rule for the authenticated instructor.
    /// </summary>
    [HttpDelete("availability/{id}")]
    public async Task<IActionResult> DeleteAvailability(int id, CancellationToken cancellationToken)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var entity = await _context.InstructorAvailabilities
            .FirstOrDefaultAsync(availability => availability.Id == id && availability.InstructorId == instructor.Id, cancellationToken);

        if (entity == null)
            return NotFound(new { error = "Availability kaydi bulunamadi." });

        _context.InstructorAvailabilities.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// POST api/instructor/section/{id}/schedule/validate — validate the current instructor section schedule.
    /// </summary>
    [HttpPost("section/{id}/schedule/validate")]
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

    private Task<bool> AvailabilityRuleExistsAsync(
        int instructorId,
        UpsertInstructorAvailabilityDto request,
        int? excludedAvailabilityId,
        CancellationToken cancellationToken) =>
        _context.InstructorAvailabilities
            .AsNoTracking()
            .AnyAsync(
                availability =>
                    availability.InstructorId == instructorId
                    && availability.DayOfWeek == request.DayOfWeek
                    && availability.StartTime == request.StartTime
                    && availability.EndTime == request.EndTime
                    && availability.IsAvailable == request.IsAvailable
                    && (!excludedAvailabilityId.HasValue || availability.Id != excludedAvailabilityId.Value),
                cancellationToken);

    private static string? ValidateAvailabilityRequest(UpsertInstructorAvailabilityDto request)
    {
        if (request.EndTime <= request.StartTime)
            return "Bitis saati baslangic saatinden sonra olmalidir.";

        if (request.IsPreferred && !request.IsAvailable)
            return "Tercihli slotlar yalnizca uygun oldugu isaretlenen pencerelerde tanimlanabilir.";

        return null;
    }

    private static InstructorAvailabilityDto MapAvailability(InstructorAvailability availability) =>
        new()
        {
            Id = availability.Id,
            InstructorId = availability.InstructorId,
            DayOfWeek = availability.DayOfWeek,
            StartTime = availability.StartTime,
            EndTime = availability.EndTime,
            IsAvailable = availability.IsAvailable,
            IsPreferred = availability.IsPreferred
        };

    private static string FormatSchedule(string scheduleJson)
    {
        if (string.IsNullOrWhiteSpace(scheduleJson) || scheduleJson == "[]")
            return string.Empty;

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var slots = JsonSerializer.Deserialize<List<ScheduleSlot>>(scheduleJson, options);
            if (slots == null || slots.Count == 0) return string.Empty;

            var formatted = slots
                .OrderBy(s => s.Day)
                .ThenBy(s => s.StartTime)
                .Select(s => $"{DayOfWeekToTurkish(s.Day)} {s.StartTime:HH:mm}-{s.EndTime:HH:mm}");

            return string.Join(", ", formatted);
        }
        catch { return string.Empty; }
    }

    private static string DayOfWeekToTurkish(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Sali",
        DayOfWeek.Wednesday => "Carsamba",
        DayOfWeek.Thursday => "Persembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        DayOfWeek.Sunday => "Pazar",
        _ => day.ToString()
    };
}
