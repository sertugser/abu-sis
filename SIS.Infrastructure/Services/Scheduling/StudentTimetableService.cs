using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class StudentTimetableService : IStudentTimetableService
{
    private readonly AppDbContext _db;
    private readonly ISystemConfigProvider _systemConfig;

    public StudentTimetableService(AppDbContext db, ISystemConfigProvider systemConfig)
    {
        _db = db;
        _systemConfig = systemConfig;
    }

    public async Task<StudentTimetableDto> GetTimetableAsync(
        int studentId,
        int? termId,
        CancellationToken cancellationToken)
    {
        var effectiveTermId = termId ?? await ResolveDefaultTermIdAsync(studentId, cancellationToken);
        var (lunchStart, lunchEnd) = await GetLunchBreakAsync(cancellationToken);
        var slots = await GetTermSlotsAsync(effectiveTermId, cancellationToken);

        var student = await _db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken)
            ?? throw new InvalidOperationException($"Öğrenci bulunamadı: {studentId}");

        var studentUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == student.UserId, cancellationToken);

        var term = await _db.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == effectiveTermId, cancellationToken);

        if (term is null)
        {
            return new StudentTimetableDto(
                StudentName: BuildStudentName(studentUser),
                TermId: 0,
                TermName: string.Empty,
                Sections: new List<StudentTimetableSectionDto>(),
                LunchBreakStart: lunchStart,
                LunchBreakEnd: lunchEnd,
                Slots: slots);
        }

        var registrations = await _db.CourseRegistrations
            .AsNoTracking()
            .Include(r => r.CourseSection).ThenInclude(cs => cs.Course)
            .Include(r => r.CourseSection).ThenInclude(cs => cs.Instructor!)
            .Include(r => r.CourseSection).ThenInclude(cs => cs.SectionSchedules)
                .ThenInclude(ss => ss.TimeSlot)
            .Include(r => r.CourseSection).ThenInclude(cs => cs.SectionSchedules)
                .ThenInclude(ss => ss.Classroom!)
            .Where(r => r.StudentId == studentId
                     && r.CourseSection.TermId == effectiveTermId
                     && r.Status == RegistrationStatus.Active
                     && r.CourseSection.Status == CourseSectionStatus.Active)
            .ToListAsync(cancellationToken);

        // Instructor adlarını ApplicationUser üzerinden çek (Instructor.User navigation yok)
        var instructorUserIds = registrations
            .Where(r => r.CourseSection.Instructor != null)
            .Select(r => r.CourseSection.Instructor!.UserId)
            .Distinct()
            .ToList();

        var instructorUsers = instructorUserIds.Count == 0
            ? new Dictionary<int, ApplicationUser>()
            : await _db.Users
                .AsNoTracking()
                .Where(u => instructorUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken);

        var sections = registrations
            .Select(r =>
            {
                ApplicationUser? instructorUser = null;
                if (r.CourseSection.Instructor != null)
                    instructorUsers.TryGetValue(r.CourseSection.Instructor.UserId, out instructorUser);
                return new StudentTimetableSectionDto(
                    SectionId: r.CourseSection.Id,
                    SectionNumber: r.CourseSection.SectionNumber.ToString(),
                    CourseCode: r.CourseSection.Course.Code,
                    CourseName: r.CourseSection.Course.Name,
                    InstructorDisplay: BuildInstructorDisplay(r.CourseSection.Instructor, instructorUser),
                    Blocks: ExpandSchedulesToSlots(r.CourseSection.SectionSchedules));
            })
            .OrderBy(s => s.CourseCode)
            .ThenBy(s => s.SectionNumber)
            .ToList();

        return new StudentTimetableDto(
            StudentName: BuildStudentName(studentUser),
            TermId: term.Id,
            TermName: term.Name,
            Sections: sections,
            LunchBreakStart: lunchStart,
            LunchBreakEnd: lunchEnd,
            Slots: slots);
    }

    /// <summary>
    /// Term'in tanımlı ders saati pencerelerini (distinct start-end) "HH:mm" olarak döner.
    /// Takvim ızgarasının satır yapısı bundan kurulur (hardcoded saat yok).
    /// </summary>
    private async Task<List<TimetableSlotDto>> GetTermSlotsAsync(int termId, CancellationToken ct)
    {
        var raw = await _db.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .Select(ts => new { ts.StartTime, ts.EndTime })
            .Distinct()
            .ToListAsync(ct);

        return raw
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.EndTime)
            .Select(s => new TimetableSlotDto(s.StartTime.ToString("HH:mm"), s.EndTime.ToString("HH:mm")))
            .ToList();
    }

    /// <summary>
    /// Sistem ayarından öğle arası penceresini okur (Schedule.LunchBreakStart/End),
    /// "HH:mm" formatına normalize eder. Ayar yoksa Ankara Bilim varsayılanı 11:40-12:20.
    /// </summary>
    private async Task<(string Start, string End)> GetLunchBreakAsync(CancellationToken ct)
    {
        var startRaw = await _systemConfig.GetStringAsync("Schedule.LunchBreakStart", "11:40", ct);
        var endRaw = await _systemConfig.GetStringAsync("Schedule.LunchBreakEnd", "12:20", ct);
        return (NormalizeTime(startRaw, "11:40"), NormalizeTime(endRaw, "12:20"));
    }

    private static string NormalizeTime(string raw, string fallback)
    {
        return TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString("HH:mm", CultureInfo.InvariantCulture)
            : fallback;
    }

    public async Task<List<StudentTimetableTermDto>> GetEnrolledTermsAsync(
        int studentId,
        CancellationToken cancellationToken)
    {
        var activeTerm = await _db.AcademicTerms
            .AsNoTracking()
            .ActiveTerm()
            .FirstOrDefaultAsync(cancellationToken);

        var enrolledTermIds = await _db.CourseRegistrations
            .AsNoTracking()
            .Where(r => r.StudentId == studentId
                     && r.Status == RegistrationStatus.Active)
            .Select(r => r.CourseSection.TermId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeId = activeTerm?.Id ?? 0;

        return await _db.AcademicTerms
            .AsNoTracking()
            .Where(t => enrolledTermIds.Contains(t.Id))
            .OrderByDescending(t => t.TermStart)
            .Select(t => new StudentTimetableTermDto(t.Id, t.Name, t.Id == activeId))
            .ToListAsync(cancellationToken);
    }

    public async Task<byte[]> GeneratePdfAsync(int studentId, int termId, CancellationToken cancellationToken)
    {
        var data = await GetTimetableAsync(studentId, termId, cancellationToken);
        var model = new Reports.TimetablePdfData(
            Title: "Öğrenci Ders Programı",
            SubjectName: data.StudentName,
            TermName: data.TermName,
            Sections: data.Sections
                .Select(s => new Reports.TimetablePdfSection(
                    s.SectionNumber,
                    s.CourseCode,
                    s.Blocks
                        .Select(b => new Reports.TimetablePdfBlock(
                            b.Day, b.StartTime, b.EndTime, b.ClassroomName,
                            b.BlockType == ScheduleBlockType.Lab))
                        .ToList()))
                .ToList());
        return new Reports.TimetablePdfRenderer().Render(model);
    }

    private async Task<int> ResolveDefaultTermIdAsync(int studentId, CancellationToken cancellationToken)
    {
        var activeTerm = await _db.AcademicTerms
            .AsNoTracking()
            .ActiveTerm()
            .FirstOrDefaultAsync(cancellationToken);
        if (activeTerm is not null) return activeTerm.Id;

        var lastEnrolledTermId = await _db.CourseRegistrations
            .AsNoTracking()
            .Where(r => r.StudentId == studentId && r.Status == RegistrationStatus.Active)
            .Join(_db.AcademicTerms,
                  r => r.CourseSection.TermId,
                  t => t.Id,
                  (r, t) => new { t.Id, t.TermStart })
            .OrderByDescending(x => x.TermStart)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return lastEnrolledTermId ?? 0;
    }

    private static string BuildStudentName(ApplicationUser? user)
    {
        if (user is null) return string.Empty;
        return $"{user.FirstName} {user.LastName}".Trim();
    }

    private static string BuildInstructorDisplay(Instructor? instructor, ApplicationUser? user)
    {
        if (instructor is null) return string.Empty;
        var firstName = user?.FirstName ?? string.Empty;
        var lastName = user?.LastName ?? string.Empty;
        var parts = new[] { instructor.Title ?? string.Empty, firstName, lastName }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" ", parts).Trim();
    }

    /// <summary>
    /// Öğrenci ders programı için ham TimeSlot bazlı listeye dönüştürür.
    /// Ortak <see cref="ScheduleHelper.BuildScheduleBlocks"/> ardışık slotları
    /// birleştirir; öğrenci sayfası 09:00-09:50 / 10:00-10:50 gibi her dilimi
    /// ayrı satırda gösterdiği için merge yapılmaz.
    /// </summary>
    private static List<ScheduleBlockDto> ExpandSchedulesToSlots(IEnumerable<SectionSchedule> schedules)
    {
        return schedules
            .OrderBy(ss => ss.TimeSlot.DayOfWeek)
            .ThenBy(ss => ss.TimeSlot.StartTime)
            .Select(ss => new ScheduleBlockDto
            {
                Day = (int)ss.TimeSlot.DayOfWeek,
                StartTime = ss.TimeSlot.StartTime,
                EndTime = ss.TimeSlot.EndTime,
                BlockType = ss.BlockType,
                ClassroomId = ss.ClassroomId,
                ClassroomName = ss.Classroom?.Name,
                ClassroomType = ss.Classroom?.Type.ToString(),
            })
            .ToList();
    }
}
