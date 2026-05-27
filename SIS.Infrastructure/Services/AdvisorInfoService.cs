using Microsoft.EntityFrameworkCore;
using SIS.Application.AdvisorInfo.DTOs;
using SIS.Application.AdvisorInfo.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AdvisorInfoService : IAdvisorInfoService
{
    private readonly AppDbContext _context;

    public AdvisorInfoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AdvisorInfoDto> GetMyAdvisorAsync(int userId, CancellationToken ct = default)
    {
        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.Advisor)
                .ThenInclude(a => a!.Department)
                    .ThenInclude(d => d!.Faculty)
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (student is null || student.AdvisorId is null || student.Advisor is null)
        {
            return new AdvisorInfoDto(
                HasAdvisor: false,
                Title: null, FirstName: null, LastName: null, FullName: null,
                FacultyName: null, DepartmentName: null, ProgramName: null,
                Phone: null, Email: null);
        }

        var advisorUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == student.Advisor.UserId, ct);

        var firstName = advisorUser?.FirstName ?? string.Empty;
        var lastName = advisorUser?.LastName ?? string.Empty;
        var title = student.Advisor.Title;

        var fullName = string.IsNullOrWhiteSpace(title)
            ? $"{firstName} {lastName}".Trim()
            : $"{title} {firstName} {lastName}".Trim();

        var mainProgram = student.StudentPrograms
            .FirstOrDefault(sp => sp.IsActive && sp.Type == StudentProgramType.Main)
            ?.Program;

        return new AdvisorInfoDto(
            HasAdvisor: true,
            Title: title,
            FirstName: firstName,
            LastName: lastName,
            FullName: fullName,
            FacultyName: student.Advisor.Department?.Faculty?.Name,
            DepartmentName: student.Advisor.Department?.Name,
            ProgramName: mainProgram?.Name,
            Phone: advisorUser?.PhoneNumber,
            Email: advisorUser?.Email);
    }

    public async Task<AdvisorScheduleDto> GetAdvisorScheduleAsync(int userId, CancellationToken ct = default)
    {
        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (student is null)
            throw new KeyNotFoundException($"Öğrenci kaydı bulunamadı (userId={userId}).");

        if (student.AdvisorId is null)
            throw new InvalidOperationException("Danışman atanmamış.");

        var now = DateTime.UtcNow;
        var term = await _context.AcademicTerms
            .AsNoTracking()
            .ActiveTerm()
            .FirstOrDefaultAsync(ct);

        if (term is null)
        {
            return new AdvisorScheduleDto(TermLabel: null, Items: Array.Empty<AdvisorScheduleItemDto>());
        }

        // Explicit Include chain — EF InMemory bazen projection içindeki navigation traverse'leri çözmüyor.
        var schedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(ss => ss.TimeSlot)
            .Include(ss => ss.Classroom)
            .Where(ss => ss.CourseSection.InstructorId == student.AdvisorId
                      && ss.CourseSection.TermId == term.Id
                      && ss.CourseSection.Status == CourseSectionStatus.Active)
            .ToListAsync(ct);

        var items = schedules
            .Select(ss => new AdvisorScheduleItemDto(
                SectionId: ss.CourseSectionId,
                CourseCode: ss.CourseSection.Course.Code,
                CourseName: ss.CourseSection.Course.Name,
                SectionNumber: ss.CourseSection.SectionNumber,
                // System.DayOfWeek: Sunday=0..Saturday=6 → 1..7 (Pzt=1, Paz=7)
                DayOfWeek: ss.TimeSlot.DayOfWeek == System.DayOfWeek.Sunday
                    ? 7
                    : (int)ss.TimeSlot.DayOfWeek,
                StartTime: ss.TimeSlot.StartTime,
                EndTime: ss.TimeSlot.EndTime,
                // Classroom kodu olarak Name kullanılır (bu sistemde Classroom.Code ayrı bir alan değil; Name = "A101" gibi kısa tanımlayıcı).
                ClassroomCode: ss.Classroom?.Name,
                BlockType: ss.BlockType.ToString()))
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTime)
            .ToList();

        return new AdvisorScheduleDto(TermLabel: term.Name, Items: items);
    }
}
