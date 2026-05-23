using Microsoft.EntityFrameworkCore;
using SIS.Application.OpenedDepartmentCourses.DTOs;
using SIS.Application.OpenedDepartmentCourses.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class OpenedDepartmentCourseService : IOpenedDepartmentCourseService
{
    private readonly AppDbContext _context;

    public OpenedDepartmentCourseService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<OpenedDepartmentCoursesDto> GetForStudentAsync(int userId, CancellationToken ct = default)
    {
        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (student is null)
        {
            return new OpenedDepartmentCoursesDto(TermLabel: null, DepartmentName: null, Items: Array.Empty<OpenedDepartmentCourseDto>());
        }

        var mainProgram = student.StudentPrograms
            .FirstOrDefault(sp => sp.IsActive && sp.Type == StudentProgramType.Main);

        if (mainProgram?.Program?.Department is null)
        {
            return new OpenedDepartmentCoursesDto(TermLabel: null, DepartmentName: null, Items: Array.Empty<OpenedDepartmentCourseDto>());
        }

        var departmentId = mainProgram.Program.DepartmentId;
        var departmentName = mainProgram.Program.Department.Name;

        var now = DateTime.UtcNow;
        var term = await _context.AcademicTerms
            .AsNoTracking()
            .ActiveTerm()
            .FirstOrDefaultAsync(ct);

        if (term is null)
        {
            return new OpenedDepartmentCoursesDto(TermLabel: null, DepartmentName: departmentName, Items: Array.Empty<OpenedDepartmentCourseDto>());
        }

        // Öğrencinin aktif curriculum'undaki ders→sınıf eşlemesi (Sınıf kolonu için).
        // CurriculumCourse.SuggestedSemester 1-8 → ClassYear = ceil(SuggestedSemester / 2).
        var curriculumCourseMap = new Dictionary<int, int>();
        if (mainProgram.CurriculumId != 0)
        {
            curriculumCourseMap = await _context.CurriculumCourses
                .AsNoTracking()
                .Where(cc => cc.CurriculumId == mainProgram.CurriculumId)
                .Select(cc => new { cc.CourseId, cc.SuggestedSemester })
                .ToDictionaryAsync(x => x.CourseId, x => (x.SuggestedSemester + 1) / 2, ct);
        }

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == term.Id
                      && cs.Status == CourseSectionStatus.Active
                      && cs.Course.DepartmentId == departmentId)
            .ToListAsync(ct);

        var items = sections
            .Select(cs => new OpenedDepartmentCourseDto(
                SectionId: cs.Id,
                SectionNumber: cs.SectionNumber,
                CourseCode: cs.Course.Code,
                CourseName: cs.Course.Name,
                ClassYear: curriculumCourseMap.TryGetValue(cs.CourseId, out var year) ? year : null,
                ApplicationHours: cs.Course.ApplicationHours ?? 0,
                LabHours: cs.Course.LabHours,
                TheoryHours: cs.Course.TheoryHours,
                NationalCredit: cs.Course.NationalCredit,
                Ects: cs.Course.Ects))
            .OrderBy(x => x.ClassYear ?? int.MaxValue)
            .ThenBy(x => x.CourseCode)
            .ThenBy(x => x.SectionNumber)
            .ToList();

        return new OpenedDepartmentCoursesDto(
            TermLabel: term.Name,
            DepartmentName: departmentName,
            Items: items);
    }
}
