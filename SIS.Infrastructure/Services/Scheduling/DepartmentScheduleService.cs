using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Öğrencinin kendi programının haftalık ders programını sunar.
/// Yetki / kapsam kararları:
///  - Öğrenci yalnızca KENDİ ana programını sorgulayabilir (programId mismatch → 403).
///  - Sınıf (1-6 + Hazırlık) filtresi <see cref="CurriculumCourse.SuggestedSemester"/>
///    üzerinden hesaplanır; SuggestedSemester=0 ise <see cref="CourseCodeHelper"/> fallback.
///  - Yayın gate: Active + IsEnrollmentSuspended==false + FacultyScheduleApproval
///    — <see cref="IRegistrationGate"/> ile birebir aynı kurallar.
/// </summary>
public sealed class DepartmentScheduleService : IDepartmentScheduleService
{
    private const int ClassYearMin = 1;
    private const int ClassYearMax = 6;
    private const int PrepClassYear = 0;

    private readonly AppDbContext _db;

    public DepartmentScheduleService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DepartmentScheduleContextDto> GetContextAsync(
        int studentId,
        CancellationToken cancellationToken)
    {
        var student = await _db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken)
            ?? throw new InvalidOperationException($"Öğrenci bulunamadı: {studentId}");

        var mainProgram = await _db.StudentPrograms
            .AsNoTracking()
            .Include(sp => sp.Program)
                .ThenInclude(p => p.Department)
            .Where(sp => sp.StudentId == studentId
                      && sp.IsActive
                      && sp.Type == StudentProgramType.Main)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Öğrencinin aktif ana programı yok.");

        var defaultClassYear = Math.Min(ClassYearMax, student.CompletedSemesters / 2 + 1);

        var availableClassYears = Enumerable.Range(ClassYearMin, ClassYearMax).ToList();

        var activeTerm = await _db.AcademicTerms
            .AsNoTracking()
            .ActiveTerm()
            .Select(t => new { t.Id, t.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var activeId = activeTerm?.Id ?? 0;

        // Tüm dönemleri TermStart'a göre tersten sırala. Sadece iptal edilmemişler.
        var terms = await _db.AcademicTerms
            .AsNoTracking()
            .Where(t => !t.IsCancelled)
            .OrderByDescending(t => t.TermStart)
            .Select(t => new DepartmentScheduleTermDto(t.Id, t.Name, t.Id == activeId))
            .ToListAsync(cancellationToken);

        return new DepartmentScheduleContextDto(
            ProgramId: mainProgram.ProgramId,
            ProgramName: mainProgram.Program.Name,
            DepartmentName: mainProgram.Program.Department.Name,
            DefaultClassYear: defaultClassYear,
            AvailableClassYears: availableClassYears,
            Terms: terms);
    }

    public async Task<DepartmentScheduleDto> GetScheduleAsync(
        int studentId,
        int termId,
        int programId,
        int classYear,
        CancellationToken cancellationToken)
    {
        if (classYear < PrepClassYear || classYear > ClassYearMax)
            throw new ArgumentOutOfRangeException(nameof(classYear), $"Geçersiz sınıf: {classYear}");

        // ── 1. Öğrencinin programa yetkisi — sadece kendi ana programını sorgulayabilir.
        var mainProgram = await _db.StudentPrograms
            .AsNoTracking()
            .Include(sp => sp.Program)
                .ThenInclude(p => p.Department)
            .Where(sp => sp.StudentId == studentId
                      && sp.IsActive
                      && sp.Type == StudentProgramType.Main)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Öğrencinin aktif ana programı yok.");

        if (mainProgram.ProgramId != programId)
            throw new UnauthorizedAccessException("Bu programa erişim yetkiniz yok.");

        var programInfo = mainProgram.Program;
        var departmentId = programInfo.DepartmentId;
        var facultyId = programInfo.Department.FacultyId;

        // ── 2. Dönem var mı?
        var term = await _db.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == termId, cancellationToken)
            ?? throw new InvalidOperationException($"Dönem bulunamadı: {termId}");

        // ── 3. Müfredat (programa bağlı, dönem aralığına denk gelen ya da aktif olan).
        // Basit kural: o dönemde mainProgram için aktif olan müfredat (öğrencinin müfredatı).
        var curriculumId = mainProgram.CurriculumId;

        // ── 4. Yayın gate — FacultyScheduleApproval yoksa boş döner.
        var facultyScheduleApproved = await _db.FacultyScheduleApprovals
            .AsNoTracking()
            .AnyAsync(a => a.FacultyId == facultyId && a.TermId == termId, cancellationToken);

        if (!facultyScheduleApproved)
        {
            return new DepartmentScheduleDto(
                TermId: term.Id,
                TermName: term.Name,
                ProgramId: programInfo.Id,
                ProgramName: programInfo.Name,
                DepartmentName: programInfo.Department.Name,
                ClassYear: classYear,
                Sections: new List<DepartmentScheduleSectionDto>());
        }

        // ── 5. Müfredat dersleri (SuggestedSemester ile birlikte) — sınıf filtresi için.
        var curriculumCourses = await _db.CurriculumCourses
            .AsNoTracking()
            .Where(cc => cc.CurriculumId == curriculumId)
            .Select(cc => new { cc.CourseId, cc.SuggestedSemester })
            .ToListAsync(cancellationToken);

        if (curriculumCourses.Count == 0)
        {
            return new DepartmentScheduleDto(
                TermId: term.Id,
                TermName: term.Name,
                ProgramId: programInfo.Id,
                ProgramName: programInfo.Name,
                DepartmentName: programInfo.Department.Name,
                ClassYear: classYear,
                Sections: new List<DepartmentScheduleSectionDto>());
        }

        // ── 6. Section'ları çek — Active + !Suspended + müfredat içinde.
        var curriculumCourseIds = curriculumCourses.Select(cc => cc.CourseId).ToHashSet();

        var sections = await _db.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor!)
            .Include(cs => cs.SectionSchedules)
                .ThenInclude(ss => ss.TimeSlot)
            .Include(cs => cs.SectionSchedules)
                .ThenInclude(ss => ss.Classroom!)
            .Where(cs => cs.TermId == termId
                      && cs.Status == CourseSectionStatus.Active
                      && !cs.IsEnrollmentSuspended
                      && curriculumCourseIds.Contains(cs.CourseId))
            .ToListAsync(cancellationToken);

        // ── 7. Instructor adlarını ApplicationUser üzerinden çek (Instructor.User navigation yok).
        var instructorUserIds = sections
            .Where(s => s.Instructor != null)
            .Select(s => s.Instructor!.UserId)
            .Distinct()
            .ToList();

        var instructorUsers = instructorUserIds.Count == 0
            ? new Dictionary<int, ApplicationUser>()
            : await _db.Users
                .AsNoTracking()
                .Where(u => instructorUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken);

        // ── 8. Classroom kapasiteleri için ek lookup (SectionSchedules.Classroom Capacity kolonu var mı?).
        // Classroom entity Capacity kolonunu DepartmentScheduleBlockDto'ya çıkartıyoruz.
        var sectionMetaBySectionId = curriculumCourses.ToDictionary(cc => cc.CourseId);

        // ── 9. Sınıf filtresi: SuggestedSemester'a göre. SuggestedSemester=0 ise CourseCode fallback.
        var filteredSections = sections
            .Where(s =>
            {
                if (!sectionMetaBySectionId.TryGetValue(s.CourseId, out var meta))
                    return false;
                var resolved = CourseCodeHelper.ResolveClassYear(meta.SuggestedSemester, s.Course.Code);
                return resolved == classYear;
            })
            .ToList();

        // ── 10. DTO build.
        var sectionDtos = filteredSections
            .Select(s =>
            {
                ApplicationUser? instructorUser = null;
                if (s.Instructor != null)
                    instructorUsers.TryGetValue(s.Instructor.UserId, out instructorUser);

                var blocks = BuildBlocks(s.SectionSchedules);

                return new DepartmentScheduleSectionDto(
                    SectionId: s.Id,
                    SectionNumber: s.SectionNumber.ToString(),
                    CourseCode: s.Course.Code,
                    CourseName: s.Course.Name,
                    InstructorDisplay: BuildInstructorDisplay(s.Instructor, instructorUser),
                    Blocks: blocks);
            })
            .OrderBy(s => s.CourseCode)
            .ThenBy(s => s.SectionNumber)
            .ToList();

        return new DepartmentScheduleDto(
            TermId: term.Id,
            TermName: term.Name,
            ProgramId: programInfo.Id,
            ProgramName: programInfo.Name,
            DepartmentName: programInfo.Department.Name,
            ClassYear: classYear,
            Sections: sectionDtos);
    }

    public async Task<byte[]> GeneratePdfAsync(
        int studentId,
        int termId,
        int programId,
        int classYear,
        CancellationToken cancellationToken)
    {
        var data = await GetScheduleAsync(studentId, termId, programId, classYear, cancellationToken);
        var renderer = new Reports.DepartmentSchedulePdfRenderer();
        return renderer.Render(data);
    }

    private static List<DepartmentScheduleBlockDto> BuildBlocks(IEnumerable<SectionSchedule> schedules)
    {
        var blocks = new List<DepartmentScheduleBlockDto>();
        var ordered = schedules
            .OrderBy(ss => ss.TimeSlot.DayOfWeek)
            .ThenBy(ss => ss.TimeSlot.StartTime)
            .ToList();

        foreach (var ss in ordered)
        {
            var day = ss.TimeSlot.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)ss.TimeSlot.DayOfWeek;

            if (blocks.Count > 0)
            {
                var last = blocks[^1];
                // Bitişik slotları birleştir (aynı gün, derslik, blok tipi).
                if (last.Day == day
                    && last.ClassroomId == ss.ClassroomId
                    && last.BlockType == ss.BlockType
                    && ss.TimeSlot.StartTime <= last.EndTime.AddMinutes(10))
                {
                    blocks[^1] = last with { EndTime = ss.TimeSlot.EndTime > last.EndTime ? ss.TimeSlot.EndTime : last.EndTime };
                    continue;
                }
            }

            blocks.Add(new DepartmentScheduleBlockDto(
                Day: day,
                StartTime: ss.TimeSlot.StartTime,
                EndTime: ss.TimeSlot.EndTime,
                BlockType: ss.BlockType,
                ClassroomId: ss.ClassroomId,
                ClassroomName: ss.Classroom?.Name,
                ClassroomCapacity: ss.Classroom?.Capacity));
        }

        return blocks;
    }

    private static string BuildInstructorDisplay(Instructor? instructor, ApplicationUser? user)
    {
        if (instructor is null) return "Atanmamış";
        var firstName = user?.FirstName ?? string.Empty;
        var lastName = user?.LastName ?? string.Empty;
        var parts = new[] { instructor.Title ?? string.Empty, firstName, lastName }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var name = string.Join(" ", parts).Trim();
        return string.IsNullOrEmpty(name) ? "Atanmamış" : name;
    }
}
