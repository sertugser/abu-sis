using Microsoft.EntityFrameworkCore;
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

    public StudentTimetableService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<StudentTimetableDto> GetTimetableAsync(
        int studentId,
        int? termId,
        CancellationToken cancellationToken)
    {
        var effectiveTermId = termId ?? await ResolveDefaultTermIdAsync(studentId, cancellationToken);

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
                Sections: new List<StudentTimetableSectionDto>());
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
                    Blocks: ScheduleHelper.BuildScheduleBlocks(r.CourseSection.SectionSchedules));
            })
            .OrderBy(s => s.CourseCode)
            .ThenBy(s => s.SectionNumber)
            .ToList();

        return new StudentTimetableDto(
            StudentName: BuildStudentName(studentUser),
            TermId: term.Id,
            TermName: term.Name,
            Sections: sections);
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

    public Task<byte[]> GeneratePdfAsync(int studentId, int termId, CancellationToken cancellationToken)
    {
        // Task 4'te PDF renderer ile doldurulacak.
        throw new NotImplementedException("PDF rendering Task 4'te eklenecek.");
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
}
