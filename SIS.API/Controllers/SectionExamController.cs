using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.Application.Grading;
using SIS.Domain.Entities;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;
using System.Security.Claims;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/instructor/section/{sectionId:int}/exams")]
[Authorize]
public class SectionExamController(AppDbContext context, ISystemConfigProvider config, IExamRegulationResolver resolver) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly ISystemConfigProvider _config = config;
    private readonly IExamRegulationResolver _resolver = resolver;

    [HttpGet]
    [RequirePermission("menu.sinav-tanimlama.view")]
    public async Task<IActionResult> List(int sectionId, CancellationToken ct)
    {
        var section = await GetAuthorizedSectionAsync(sectionId, ct);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasında bulunamadı." });

        var exams = await _context.SectionExams
            .AsNoTracking()
            .Include(se => se.GradeComponent)
            .Include(se => se.Classrooms).ThenInclude(c => c.Classroom)
            .Where(se => se.CourseSectionId == sectionId)
            .OrderBy(se => se.ExamDate)
            .ToListAsync(ct);

        return Ok(exams.Select(ToDto).ToList());
    }

    [HttpPost]
    [RequirePermission("menu.sinav-tanimlama.manage")]
    public async Task<IActionResult> Upsert(int sectionId, [FromBody] SaveSectionExamRequest req, CancellationToken ct)
    {
        var section = await GetAuthorizedSectionAsync(sectionId, ct);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasında bulunamadı." });

        if (req.GradeComponentId <= 0)
            return BadRequest(new { error = "Geçersiz sınav kalemi." });
        if (req.ExamDate == default)
            return BadRequest(new { error = "Sınav tarihi geçerli değil." });
        if (req.DurationMinutes is <= 0)
            return BadRequest(new { error = "Sınav süresi pozitif olmalıdır." });
        if (req.Description is { Length: > 500 })
            return BadRequest(new { error = "Açıklama 500 karakteri aşamaz." });

        var component = await _context.GradeComponents.FirstOrDefaultAsync(
            gc => gc.Id == req.GradeComponentId
                && gc.CourseId == section.CourseId
                && gc.AcademicTermId == section.TermId, ct);
        if (component == null)
            return BadRequest(new { error = "Seçilen sınav kalemi bu derse ait değil." });

        var classroomIds = (req.ClassroomIds ?? Array.Empty<int>()).Distinct().ToList();
        if (classroomIds.Count > 0)
        {
            var existingCount = await _context.Classrooms.CountAsync(c => classroomIds.Contains(c.Id), ct);
            if (existingCount != classroomIds.Count)
                return BadRequest(new { error = "Geçersiz derslik seçimi." });
        }

        var conflict = await EvaluateConflictAsync(section, req, classroomIds, component.Name, ct);
        if (conflict.Kind != ExamConflictKind.None)
            return Conflict(new { error = conflict.Message });

        SectionExam exam;
        if (req.Id.HasValue)
        {
            var existingExam = await _context.SectionExams
                .Include(se => se.Classrooms)
                .FirstOrDefaultAsync(se => se.Id == req.Id.Value && se.CourseSectionId == sectionId, ct);
            if (existingExam == null)
                return NotFound(new { error = "Sınav oturumu bulunamadı." });
            exam = existingExam;

            exam.GradeComponentId = req.GradeComponentId;
            exam.ExamDate = req.ExamDate;
            exam.DurationMinutes = req.DurationMinutes;
            exam.Description = req.Description;
            if (req.IsAnnouncedToStudents && !exam.IsAnnouncedToStudents)
                exam.AnnouncedAt = DateTime.UtcNow;
            else if (!req.IsAnnouncedToStudents)
                exam.AnnouncedAt = null;
            exam.IsAnnouncedToStudents = req.IsAnnouncedToStudents;
            exam.UpdatedAt = DateTime.UtcNow;

            _context.SectionExamClassrooms.RemoveRange(exam.Classrooms);
            exam.Classrooms = classroomIds.Select(id => new SectionExamClassroom { ClassroomId = id }).ToList();
        }
        else
        {
            exam = new SectionExam
            {
                CourseSectionId = sectionId,
                GradeComponentId = req.GradeComponentId,
                ExamDate = req.ExamDate,
                DurationMinutes = req.DurationMinutes,
                IsAnnouncedToStudents = req.IsAnnouncedToStudents,
                AnnouncedAt = req.IsAnnouncedToStudents ? DateTime.UtcNow : null,
                Description = req.Description,
                CreatedAt = DateTime.UtcNow,
                Classrooms = classroomIds.Select(id => new SectionExamClassroom { ClassroomId = id }).ToList()
            };
            _context.SectionExams.Add(exam);
        }

        await _context.SaveChangesAsync(ct);

        var saved = await _context.SectionExams
            .AsNoTracking()
            .Include(se => se.GradeComponent)
            .Include(se => se.Classrooms).ThenInclude(c => c.Classroom)
            .FirstAsync(se => se.Id == exam.Id, ct);
        return Ok(ToDto(saved));
    }

    [HttpDelete("{examId:int}")]
    [RequirePermission("menu.sinav-tanimlama.manage")]
    public async Task<IActionResult> Delete(int sectionId, int examId, CancellationToken ct)
    {
        var section = await GetAuthorizedSectionAsync(sectionId, ct);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasında bulunamadı." });

        var exam = await _context.SectionExams
            .Include(se => se.Classrooms)
            .FirstOrDefaultAsync(se => se.Id == examId && se.CourseSectionId == sectionId, ct);
        if (exam == null)
            return NotFound(new { error = "Sınav oturumu bulunamadı." });

        _context.SectionExamClassrooms.RemoveRange(exam.Classrooms);
        _context.SectionExams.Remove(exam);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("config")]
    [RequirePermission("menu.sinav-tanimlama.view")]
    public async Task<IActionResult> GetExamConfig(int sectionId, CancellationToken ct)
    {
        var section = await GetAuthorizedSectionAsync(sectionId, ct);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasında bulunamadı." });

        var components = await _context.GradeComponents
            .AsNoTracking()
            .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
            .OrderBy(gc => gc.Order)
            .Select(gc => new ExamConfigComponentDto(
                gc.Id, gc.Name, gc.ShortName, gc.Type.ToString(), gc.Weight, gc.Order))
            .ToListAsync(ct);

        var counts = await _resolver.ResolveCourseExamCountsAsync(section.CourseId, section.TermId, ct);
        var bounds = await _resolver.ResolveCourseWeightBoundsAsync(section.CourseId, section.TermId, ct);

        var classrooms = await _context.Classrooms
            .AsNoTracking()
            .OrderBy(c => c.BuildingName).ThenBy(c => c.Name)
            .Select(c => new ExamConfigClassroomDto(c.Id, c.Name, c.BuildingName, c.Capacity))
            .ToListAsync(ct);

        return Ok(new ExamConfigDto(
            section.Course.Code,
            section.Course.Name,
            counts.HasRegulation,
            counts.MidtermCount,
            counts.FinalCount,
            bounds?.MinMidterm, bounds?.MaxMidterm, bounds?.MinFinal, bounds?.MaxFinal,
            components,
            classrooms));
    }

    private async Task<CourseSection?> GetAuthorizedSectionAsync(int sectionId, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.UserId == userId, ct);
        if (instructor == null) return null;

        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == sectionId, ct);
        if (section == null) return null;

        return section.InstructorId == instructor.Id ? section : null;
    }

    private static SectionExamDto ToDto(SectionExam se) => new(
        se.Id,
        se.GradeComponentId,
        se.GradeComponent?.Name ?? "",
        se.GradeComponent?.Type.ToString() ?? "Other",
        se.GradeComponent?.Weight ?? 0,
        se.ExamDate,
        se.DurationMinutes,
        se.IsAnnouncedToStudents,
        se.AnnouncedAt,
        se.Description,
        se.Classrooms.Select(c => new SectionExamClassroomDto(c.ClassroomId, c.Classroom?.Name ?? "")).ToList());

    /// <summary>
    /// Aynı dönemdeki diğer sınavlara karşı oda + kohort çakışmasını değerlendirir.
    /// Kohort = (program, sınıf-yılı); sınıf-yılı CourseCodeHelper.ResolveClassYear ile.
    /// </summary>
    private async Task<ExamConflictResult> EvaluateConflictAsync(
        CourseSection section, SaveSectionExamRequest req, IReadOnlyList<int> classroomIds,
        string componentName, CancellationToken ct)
    {
        var defaultMinutes = await _config.GetIntAsync("Exam.DefaultDurationMinutes", 60, ct);

        var others = await _context.SectionExams
            .AsNoTracking()
            .Include(se => se.Classrooms)
            .Include(se => se.GradeComponent)
            .Include(se => se.CourseSection).ThenInclude(cs => cs.Course)
            .Where(se => se.Id != (req.Id ?? 0)
                && se.CourseSection.TermId == section.TermId
                && se.ExamDate >= req.ExamDate.AddDays(-1)
                && se.ExamDate <= req.ExamDate.AddDays(1))
            .ToListAsync(ct);

        // Kohort için ders kodları (candidate + diğerleri)
        var courseCodes = new Dictionary<int, string> { [section.CourseId] = section.Course.Code };
        foreach (var o in others)
            courseCodes[o.CourseSection.CourseId] = o.CourseSection.Course.Code;

        var cohortMap = await BuildCohortsAsync(courseCodes, ct);

        var candidate = new ExamConflictInput(
            req.Id ?? 0,
            req.ExamDate,
            req.ExamDate.AddMinutes(req.DurationMinutes ?? defaultMinutes),
            classroomIds,
            cohortMap.GetValueOrDefault(section.CourseId, new List<ExamCohort>()),
            $"{section.Course.Code} {componentName}");

        var existing = others.Select(o => new ExamConflictInput(
            o.Id,
            o.ExamDate,
            o.ExamDate.AddMinutes(o.DurationMinutes ?? defaultMinutes),
            o.Classrooms.Select(c => c.ClassroomId).ToList(),
            cohortMap.GetValueOrDefault(o.CourseSection.CourseId, new List<ExamCohort>()),
            $"{o.CourseSection.Course.Code} {o.GradeComponent?.Name} ({o.ExamDate:dd.MM HH:mm})")).ToList();

        return ExamConflictEvaluator.Evaluate(candidate, existing);
    }

    /// <summary>courseId → (program, sınıf-yılı) kohort listesi. Müfredata bağlı olmayan dersler boş döner.</summary>
    private async Task<Dictionary<int, List<ExamCohort>>> BuildCohortsAsync(
        IReadOnlyDictionary<int, string> courseCodes, CancellationToken ct)
    {
        var courseIds = courseCodes.Keys.ToList();
        var rows = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => courseIds.Contains(cc.CourseId))
            .Select(cc => new { cc.CourseId, cc.SuggestedSemester, cc.Curriculum.ProgramId })
            .ToListAsync(ct);

        var map = new Dictionary<int, List<ExamCohort>>();
        foreach (var courseId in courseIds)
            map[courseId] = new List<ExamCohort>();

        foreach (var r in rows)
        {
            var year = CourseCodeHelper.ResolveClassYear(r.SuggestedSemester, courseCodes.GetValueOrDefault(r.CourseId, ""));
            var list = map[r.CourseId];
            if (!list.Any(c => c.ProgramId == r.ProgramId && c.ClassYear == year))
                list.Add(new ExamCohort(r.ProgramId, year));
        }
        return map;
    }
}

public record SectionExamClassroomDto(int ClassroomId, string ClassroomName);

public record SectionExamDto(
    int Id,
    int GradeComponentId,
    string ComponentName,
    string ComponentType,
    decimal Weight,
    DateTime ExamDate,
    int? DurationMinutes,
    bool IsAnnouncedToStudents,
    DateTime? AnnouncedAt,
    string? Description,
    IReadOnlyList<SectionExamClassroomDto> Classrooms);

public record SaveSectionExamRequest(
    int? Id,
    int GradeComponentId,
    DateTime ExamDate,
    int? DurationMinutes,
    bool IsAnnouncedToStudents,
    string? Description,
    IReadOnlyList<int> ClassroomIds);

public record ExamConfigComponentDto(int Id, string Name, string ShortName, string Type, decimal Weight, int Order);
public record ExamConfigClassroomDto(int Id, string Name, string BuildingName, int Capacity);
public record ExamConfigDto(
    string CourseCode, string CourseName,
    bool HasRegulation, int MidtermCount, int FinalCount,
    decimal? MinMidtermWeight, decimal? MaxMidtermWeight, decimal? MinFinalWeight, decimal? MaxFinalWeight,
    IReadOnlyList<ExamConfigComponentDto> Components,
    IReadOnlyList<ExamConfigClassroomDto> Classrooms);
