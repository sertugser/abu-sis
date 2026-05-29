using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIS.Application.OpenedDepartmentCourses.DTOs;
using SIS.Application.OpenedDepartmentCourses.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class OpenedDepartmentCourseService : IOpenedDepartmentCourseService
{
    private readonly AppDbContext _context;
    private readonly ILogger<OpenedDepartmentCourseService> _logger;

    public OpenedDepartmentCourseService(
        AppDbContext context,
        ILogger<OpenedDepartmentCourseService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OpenedDepartmentCoursesDto> GetForStudentAsync(int userId, CancellationToken ct = default)
    {
        // ── 1. Öğrenci + aktif Main program (+ Curriculum + Department) ────
        var studentInfo = await _context.Students
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new
            {
                StudentId = s.Id,
                MainProgram = s.StudentPrograms
                    .Where(sp => sp.IsActive && sp.Type == StudentProgramType.Main)
                    .Select(sp => new
                    {
                        sp.CurriculumId,
                        sp.ProgramId,
                        DepartmentId = sp.Program.DepartmentId,
                        DepartmentName = sp.Program.Department.Name,
                        FacultyId = sp.Program.Department.FacultyId
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (studentInfo is null)
        {
            _logger.LogInformation("OpenedDepartmentCourses: userId={UserId} için öğrenci kaydı bulunamadı.", userId);
            return Empty(OpenedDepartmentCoursesEmptyReason.StudentNotFound, termLabel: null, departmentName: null);
        }

        if (studentInfo.MainProgram is null)
        {
            _logger.LogWarning("OpenedDepartmentCourses: studentId={StudentId} için aktif Main program yok.", studentInfo.StudentId);
            return Empty(OpenedDepartmentCoursesEmptyReason.NoMainProgram, termLabel: null, departmentName: null);
        }

        var curriculumId = studentInfo.MainProgram.CurriculumId;
        var departmentName = studentInfo.MainProgram.DepartmentName;
        var facultyId = studentInfo.MainProgram.FacultyId;

        // ── 2. Aktif dönem ──────────────────────────────────────────────────
        var term = await _context.AcademicTerms
            .AsNoTracking()
            .ActiveTerm()
            .Select(t => new { t.Id, t.Name })
            .FirstOrDefaultAsync(ct);

        if (term is null)
        {
            _logger.LogInformation("OpenedDepartmentCourses: aktif dönem yok (studentId={StudentId}).", studentInfo.StudentId);
            return Empty(OpenedDepartmentCoursesEmptyReason.NoActiveTerm, termLabel: null, departmentName: departmentName);
        }

        // ── 3. Fakülte program onayı (RegistrationGate ile aynı kapı) ──────
        // Onay yoksa öğrenci hiçbir section görmemeli — RegistrationGate kayda eklemeyi
        // zaten reddediyor; burada da listeden gizleyerek UI/UX tutarsızlığı kapatılır.
        var facultyScheduleApproved = await _context.FacultyScheduleApprovals
            .AsNoTracking()
            .AnyAsync(a => a.FacultyId == facultyId && a.TermId == term.Id, ct);

        if (!facultyScheduleApproved)
        {
            _logger.LogInformation(
                "OpenedDepartmentCourses: fakülteId={FacultyId} termId={TermId} için program onayı yok — boş liste.",
                facultyId, term.Id);
            return Empty(OpenedDepartmentCoursesEmptyReason.Empty, termLabel: term.Name, departmentName: departmentName);
        }

        // ── 4. Öğrencinin müfredatındaki dersler (cross-dept dahil) ────────
        // Proliz davranışı: bölüm değil müfredat sınırı belirler. CENG446 gibi
        // başka bölümün dersi de ISE müfredatında varsa listede yer alır.
        var curriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => cc.CurriculumId == curriculumId)
            .Select(cc => new
            {
                cc.CourseId,
                cc.SuggestedSemester,
                cc.IsCompulsory
            })
            .ToListAsync(ct);

        if (curriculumCourses.Count == 0)
        {
            return Empty(OpenedDepartmentCoursesEmptyReason.Empty, termLabel: term.Name, departmentName: departmentName);
        }

        var curriculumCourseIds = curriculumCourses.Select(cc => cc.CourseId).ToHashSet();
        var courseMeta = curriculumCourses.ToDictionary(cc => cc.CourseId, cc => cc);

        // ── 5. Bu dönemde açılan + müfredattaki + onaylı + suspended değil ─
        var sections = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == term.Id
                      && cs.Status == CourseSectionStatus.Active
                      && !cs.IsEnrollmentSuspended
                      && curriculumCourseIds.Contains(cs.CourseId))
            .Select(cs => new
            {
                SectionId = cs.Id,
                cs.SectionNumber,
                cs.CourseId,
                CourseCode = cs.Course.Code,
                CourseName = cs.Course.Name,
                ApplicationHours = cs.Course.ApplicationHours ?? 0,
                cs.Course.LabHours,
                cs.Course.TheoryHours,
                cs.Course.NationalCredit,
                cs.Course.Ects
            })
            .ToListAsync(ct);

        // ── 6. DTO + sınıf yılı (kod prefix fallback) ───────────────────────
        var items = sections
            .Select(s =>
            {
                var meta = courseMeta[s.CourseId];
                var classYear = CourseCodeHelper.ResolveClassYear(meta.SuggestedSemester, s.CourseCode);
                return new OpenedDepartmentCourseDto(
                    SectionId: s.SectionId,
                    SectionNumber: s.SectionNumber,
                    CourseCode: s.CourseCode,
                    CourseName: s.CourseName,
                    ClassYear: classYear,
                    ApplicationHours: s.ApplicationHours,
                    LabHours: s.LabHours,
                    TheoryHours: s.TheoryHours,
                    IsCompulsory: meta.IsCompulsory,
                    NationalCredit: s.NationalCredit,
                    Ects: s.Ects);
            })
            .OrderBy(x => x.ClassYear)
            .ThenBy(x => x.CourseCode)
            .ThenBy(x => x.SectionNumber)
            .ToList();

        return new OpenedDepartmentCoursesDto(
            TermLabel: term.Name,
            DepartmentName: departmentName,
            EmptyReason: items.Count == 0 ? OpenedDepartmentCoursesEmptyReason.Empty : OpenedDepartmentCoursesEmptyReason.None,
            Items: items);
    }

    private static OpenedDepartmentCoursesDto Empty(
        OpenedDepartmentCoursesEmptyReason reason,
        string? termLabel,
        string? departmentName) =>
        new(termLabel, departmentName, reason, Array.Empty<OpenedDepartmentCourseDto>());
}
