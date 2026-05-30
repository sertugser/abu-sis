using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.DTOs;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CourseController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly IWebModuleControlService _webModuleControls;
    private readonly ISystemConfigProvider _systemConfig;
    private readonly IStudentRegistrationCatalogService _registrationCatalogService;
    private readonly ISectionPickerService _sectionPickerService;

    public CourseController(
        AppDbContext context,
        IDateTimeProvider clock,
        ISystemConfigProvider systemConfig,
        IStudentRegistrationCatalogService registrationCatalogService,
        ISectionPickerService sectionPickerService,
        IWebModuleControlService? webModuleControls = null)
    {
        _context = context;
        _clock = clock;
        _systemConfig = systemConfig;
        _registrationCatalogService = registrationCatalogService;
        _sectionPickerService = sectionPickerService;
        _webModuleControls = webModuleControls ?? new NullWebModuleControlService();
    }

    /// <summary>
    /// GET api/course — list courses for the active term.
    /// Optionally filter by programId query parameter.
    /// </summary>
    [HttpGet]
    [RequirePermission("lookup.course")]
    // Patch 2A: Permission kaldırıldı; data multi-role (sadece authenticated).
    public async Task<IActionResult> GetCourses([FromQuery] int? programId)
    {
        // Find active term
        var now = _clock.UtcNow;
        var activeTerm = await _context.AcademicTerms
            .ActiveTerm()
            .FirstOrDefaultAsync();

        if (activeTerm == null)
            return NotFound(new { error = "Aktif donem bulunamadi." });

        // Get sections for the active term with related data
        var sectionsQuery = _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .Where(cs => cs.TermId == activeTerm.Id);

        // If programId is provided, filter courses that belong to that program's active curriculum
        HashSet<int>? programCourseIds = null;
        Dictionary<int, bool>? compulsoryMap = null;
        Dictionary<int, int>? semesterMap = null;

        if (programId.HasValue)
        {
            var curriculumCourses = await _context.CurriculumCourses
                .Include(cc => cc.Curriculum)
                .Where(cc => cc.Curriculum.ProgramId == programId.Value && cc.Curriculum.IsActive)
                .ToListAsync();

            programCourseIds = curriculumCourses.Select(cc => cc.CourseId).ToHashSet();
            compulsoryMap = curriculumCourses.ToDictionary(cc => cc.CourseId, cc => cc.IsCompulsory);
            semesterMap = curriculumCourses.ToDictionary(cc => cc.CourseId, cc => cc.SuggestedSemester);

            sectionsQuery = sectionsQuery.Where(cs => programCourseIds.Contains(cs.CourseId));
        }

        var sections = await sectionsQuery.ToListAsync();

        // We need instructor user info for full name — load users for instructors
        var instructorIds = sections
            .Where(s => s.Instructor != null)
            .Select(s => s.Instructor!.UserId)
            .Distinct()
            .ToList();

        var userNames = await _context.Users
            .Where(u => instructorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        // Load SectionSchedule records for schedule display
        var sectionIds = sections.Select(s => s.Id).ToList();
        var sectionSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => sectionIds.Contains(ss.CourseSectionId))
            .ToListAsync();
        var scheduleMap = sectionSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => ScheduleHelper.FormatSchedule(g));

        // Group sections by course
        var courseGroups = sections
            .GroupBy(s => s.CourseId)
            .Select(g =>
            {
                var course = g.First().Course;
                return new CourseListDto
                {
                    CourseId = course.Id,
                    Code = course.Code,
                    Name = course.Name,
                    Ects = course.Ects,
                    TheoryHours = course.TheoryHours,
                    LabHours = course.LabHours,
                    IsCompulsory = compulsoryMap != null
                        && compulsoryMap.TryGetValue(course.Id, out var isComp) && isComp,
                    SuggestedSemester = semesterMap != null
                        && semesterMap.TryGetValue(course.Id, out var sem) ? sem : 0,
                    Sections = g.Select(s => new SectionDto
                    {
                        SectionId = s.Id,
                        InstructorName = s.Instructor != null && userNames.TryGetValue(s.Instructor.UserId, out var name)
                            ? $"{s.Instructor.Title} {name}".Trim()
                            : string.Empty,
                        Capacity = s.Capacity,
                        EnrolledCount = s.EnrolledCount,
                        AvailableSeats = Math.Max(0, s.Capacity - s.EnrolledCount),
                        ClassroomName = s.Classroom?.Name ?? string.Empty,
                        Schedule = scheduleMap.GetValueOrDefault(s.Id, string.Empty),
                        IsActive = s.IsActive
                    }).ToList()
                };
            })
            .OrderBy(c => c.Code)
            .ToList();

        return Ok(courseGroups);
    }

    /// <summary>GET api/course/registration-catalog — yeni müfredat-ağacı katalog (öğrenci).</summary>
    [HttpGet("registration-catalog")]
    [RequirePermission("menu.ders-kayit.view")]
    public async Task<IActionResult> GetRegistrationCatalog(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new ApiErrorResponse("Kullanici bilgisi bulunamadi.", "UserNotFound"));

        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (student == null)
            return NotFound(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);
        if (activeProgram == null)
            return NotFound(new ApiErrorResponse("Aktif program bulunamadi.", "ActiveProgramNotFound"));

        try
        {
            var dto = await _registrationCatalogService.BuildAsync(student, activeProgram, ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex) when (ex.Message == "ActiveTermNotFound")
        {
            return NotFound(new ApiErrorResponse("Aktif donem bulunamadi.", "ActiveTermNotFound"));
        }
    }

    /// <summary>GET api/course/registration-catalog/{studentId} — danışman öğrenci-adına.</summary>
    [HttpGet("registration-catalog/{studentId:int}")]
    [RequirePermission("menu.ders-kayit-duzenleme.view")]
    public async Task<IActionResult> GetRegistrationCatalogForAdvisor(int studentId, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new ApiErrorResponse("Kullanici bilgisi bulunamadi.", "UserNotFound"));

        var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.UserId == userId, ct);
        if (instructor == null)
            return StatusCode(403, new ApiErrorResponse("Bu islem icin ogretim uyesi profili gerekli.", "InstructorRequired"));

        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null)
            return NotFound(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));

        if (student.AdvisorId != instructor.Id)
            return StatusCode(403, new ApiErrorResponse("Bu ogrencinin danismani degilsiniz.", "NotAdvisee"));

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);
        if (activeProgram == null)
            return NotFound(new ApiErrorResponse("Aktif program bulunamadi.", "ActiveProgramNotFound"));

        try
        {
            var dto = await _registrationCatalogService.BuildAsync(student, activeProgram, ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex) when (ex.Message == "ActiveTermNotFound")
        {
            return NotFound(new ApiErrorResponse("Aktif donem bulunamadi.", "ActiveTermNotFound"));
        }
    }

    /// <summary>GET api/course/{courseId}/sections — "+"a basınca açılan filtreli/sayfalı şube listesi.</summary>
    [HttpGet("{courseId:int}/sections")]
    [RequirePermission("menu.ders-kayit.view")]
    public async Task<IActionResult> GetCourseSections(
        int courseId,
        [FromQuery] int? instructorId, [FromQuery] int? facultyId, [FromQuery] int? programId,
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new ApiErrorResponse("Kullanici bilgisi bulunamadi.", "UserNotFound"));

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (student == null)
            return Unauthorized(new ApiErrorResponse("Ogrenci bilgisi bulunamadi.", "StudentNotFound"));

        var result = await _sectionPickerService.QueryAsync(
            student.Id,
            new SectionPickerQuery(courseId, instructorId, facultyId, programId, q, page, pageSize),
            ct);
        return Ok(result);
    }

    /// <summary>
    /// GET api/course/sections/{id} — get section detail.
    /// </summary>
    [HttpGet("sections/{id}")]
    [RequirePermission("lookup.course")]
    // Patch 2A: Permission kaldırıldı; data multi-role (sadece authenticated).
    public async Task<IActionResult> GetSectionDetail(int id)
    {
        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == id);

        if (section == null)
            return NotFound(new { error = "Ders sectionu bulunamadi." });

        string instructorName = string.Empty;
        if (section.Instructor != null)
        {
            var user = await _context.Users.FindAsync(section.Instructor.UserId);
            if (user != null)
                instructorName = $"{section.Instructor.Title} {user.FirstName} {user.LastName}".Trim();
        }

        var detailSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => ss.CourseSectionId == section.Id)
            .ToListAsync();

        var hideQuotaInCatalog = await _systemConfig.GetBoolAsync("Registration.HideQuotaInCatalog", false);

        var dto = new SectionDto
        {
            SectionId = section.Id,
            InstructorName = instructorName,
            Capacity = hideQuotaInCatalog ? 0 : section.Capacity,
            EnrolledCount = hideQuotaInCatalog ? 0 : section.EnrolledCount,
            AvailableSeats = hideQuotaInCatalog ? 0 : Math.Max(0, section.Capacity - section.EnrolledCount),
            ClassroomName = section.Classroom?.Name ?? string.Empty,
            Schedule = ScheduleHelper.FormatSchedule(detailSchedules),
            IsActive = section.IsActive,
            QuotaVisible = !hideQuotaInCatalog
        };

        return Ok(new
        {
            Section = dto,
            Course = new
            {
                section.Course.Id,
                section.Course.Code,
                section.Course.Name,
                section.Course.Ects
            }
        });
    }

    /// <summary>
    /// GET api/course/{id}/detail — "Ders Bilgileri" görünümü için ders katalog detayı.
    /// Course tüm alanları + Prerequisites + Equivalencies + Department/Faculty bilgisi.
    /// Öğrenci "Alınan Dersler" sayfasındaki dişli ikonundan açar.
    /// </summary>
    [HttpGet("{id}/detail")]
    [RequirePermission("lookup.course")]
    public async Task<IActionResult> GetCourseDetail(int id, CancellationToken ct)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Department)
                .ThenInclude(d => d!.Faculty)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (course == null)
            return NotFound(new { error = "Ders bulunamadı." });

        var prerequisites = await _context.CoursePrerequisites
            .AsNoTracking()
            .Where(p => p.CourseId == id)
            .Join(_context.Courses, p => p.RequiredCourseId, c => c.Id, (p, c) => new
            {
                CourseId = c.Id,
                c.Code,
                c.Name
            })
            .ToListAsync(ct);

        var equivalencies = await _context.CourseEquivalencies
            .AsNoTracking()
            .Where(e => e.CourseId == id)
            .Join(_context.Courses, e => e.EquivalentCourseId, c => c.Id, (e, c) => new
            {
                CourseId = c.Id,
                c.Code,
                c.Name
            })
            .ToListAsync(ct);

        return Ok(new
        {
            Id = course.Id,
            Code = course.Code,
            Name = course.Name,
            EnglishName = course.EnglishName,
            Description = course.Description,
            Ects = course.Ects,
            NationalCredit = course.NationalCredit,
            TheoryHours = course.TheoryHours,
            LabHours = course.LabHours,
            ApplicationHours = course.ApplicationHours,
            Language = FormatCourseLanguage(course.Language),
            Level = course.Level.ToString(),
            TeachingMethod = course.TeachingMethod.ToString(),
            Status = course.Status.ToString(),
            IsNonGpa = course.IsNonGpa,
            DepartmentName = course.Department?.Name,
            FacultyName = course.Department?.Faculty?.Name,
            ExamType = course.ExamType,
            Prerequisites = prerequisites,
            Equivalencies = equivalencies
        });
    }

    private static string FormatCourseLanguage(SIS.Domain.Enums.CourseLanguage language) => language switch
    {
        SIS.Domain.Enums.CourseLanguage.Turkish => "Türkçe",
        SIS.Domain.Enums.CourseLanguage.English => "İngilizce",
        SIS.Domain.Enums.CourseLanguage.French  => "Fransızca",
        SIS.Domain.Enums.CourseLanguage.German  => "Almanca",
        SIS.Domain.Enums.CourseLanguage.Arabic  => "Arapça",
        _ => language.ToString()
    };

    /// <summary>
    /// GET api/course/terms/active — get the active term info.
    /// </summary>
    [HttpGet("terms/active")]
    [RequirePermission("lookup.term")]
    public async Task<IActionResult> GetActiveTerm()
    {
        var now = _clock.UtcNow;
        var term = await _context.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .ActiveTerm()
            .FirstOrDefaultAsync();

        if (term == null)
            return NotFound(new { error = "Aktif donem bulunamadi." });

        var registrationControlCode = await ResolveRegistrationControlCodeForCurrentUserAsync(term.Id);
        var addDropControlCode = await ResolveAddDropControlCodeForCurrentUserAsync();
        var registrationControl = await GetStudentRegistrationControlAsync(term.Id, registrationControlCode);
        var addDropControl = await GetStudentRegistrationControlAsync(term.Id, addDropControlCode);

        var dto = new ActiveTermDto
        {
            Id = term.Id,
            Name = term.Name,
            Year = term.Year,
            Semester = term.TermType.ToString(),
            RegistrationStart = term.AcademicCalendar?.RegistrationStart ?? term.TermStart,
            RegistrationEnd = term.AcademicCalendar?.RegistrationEnd ?? term.TermStart,
            AddDropStart = term.AcademicCalendar?.AddDropStart ?? term.TermStart,
            AddDropEnd = term.AcademicCalendar?.AddDropEnd ?? term.TermStart,
            IsRegistrationOpen = registrationControl.IsOpen,
            IsAddDropOpen = addDropControl.IsOpen,
            Status = term.GetStatus(_clock.UtcNow).ToString(),
            HasAcademicCalendar = term.AcademicCalendar != null,
            StudentRegistrationMessage = registrationControl.Message,
        };

        return Ok(dto);
    }

    private async Task<StudentRegistrationControl> GetStudentRegistrationControlAsync(int? termId, string controlCode)
    {
        var access = await _webModuleControls.EvaluateAsync(controlCode, termId);
        return new StudentRegistrationControl(access.IsEnabled, access.IsOpen, null, access.Message);
    }

    private async Task<string> ResolveRegistrationControlCodeForCurrentUserAsync(int termId)
    {
        var student = await LoadCurrentStudentAsync();
        if (student is null)
            return "student-course-registration";

        if (student.CompletedSemesters == 0)
            return "student-new-course-registration";

        return IsOverNormalStudyDuration(student)
            ? "student-extended-course-registration"
            : "student-course-registration";
    }

    private async Task<string> ResolveAddDropControlCodeForCurrentUserAsync()
    {
        var student = await LoadCurrentStudentAsync();
        return student?.CompletedSemesters == 0
            ? "student-new-add-drop"
            : "student-add-drop";
    }

    private async Task<SIS.Domain.Entities.Student?> LoadCurrentStudentAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        return await _context.Students
            .AsNoTracking()
            .Include(student => student.StudentPrograms)
                .ThenInclude(program => program.Program)
            .FirstOrDefaultAsync(student => student.UserId == userId);
    }

    private static bool IsOverNormalStudyDuration(SIS.Domain.Entities.Student student)
    {
        var activeProgram = student.StudentPrograms
            .FirstOrDefault(program => program.Type == StudentProgramType.Main && program.IsActive);

        if (activeProgram?.Program == null)
            return false;

        var normalDurationSemesters = activeProgram.Program.DegreeLevel switch
        {
            DegreeLevel.Associate => 4,
            DegreeLevel.Bachelor => 8,
            DegreeLevel.Master => 4,
            DegreeLevel.Doctorate => 8,
            _ => 8
        };

        return student.CompletedSemesters >= normalDurationSemesters;
    }

    private sealed record StudentRegistrationControl(bool IsEnabled, bool IsOpen, TimeOnly? OpenAt, string? Message);

}
