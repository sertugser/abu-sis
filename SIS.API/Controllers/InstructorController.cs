using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Instructor")]
public class InstructorController : ControllerBase
{
    private readonly AppDbContext _context;

    public InstructorController(AppDbContext context)
    {
        _context = context;
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

    private async Task<Domain.Entities.Instructor?> GetInstructorAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        return await _context.Instructors.FirstOrDefaultAsync(i => i.UserId == userId);
    }

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
