using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.CalendarGenerator.Services;
using SIS.Application.Common.Services;
using SIS.Application.Common.DTOs;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.StudentCard.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGpaCalculationService _gpaCalculationService;
    private readonly IAcademicCalendarRuleService _calendarRuleService;
    private readonly IWebModuleControlService _webModuleControls;
    private readonly ISystemConfigProvider _systemConfig;
    private readonly ICurriculumStatusService _curriculumStatusService;
    private readonly IStudentTranscriptService _transcriptService;

    public StudentController(
        AppDbContext context,
        IGpaCalculationService gpaCalculationService,
        IAcademicCalendarRuleService calendarRuleService,
        ISystemConfigProvider systemConfig,
        ICurriculumStatusService curriculumStatusService,
        IStudentTranscriptService transcriptService,
        IWebModuleControlService? webModuleControls = null)
    {
        _context = context;
        _gpaCalculationService = gpaCalculationService;
        _calendarRuleService = calendarRuleService;
        _systemConfig = systemConfig;
        _curriculumStatusService = curriculumStatusService;
        _transcriptService = transcriptService;
        _webModuleControls = webModuleControls ?? new NullWebModuleControlService();
    }

    /// <summary>
    /// GET api/student/me — returns the authenticated student's profile info.
    /// </summary>
    [HttpGet("me")]
    [RequirePermission("menu.ana-sayfa.view")]
    public async Task<IActionResult> GetMe()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .Include(s => s.Advisor)
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (student == null)
            return NotFound(new { error = "Ogrenci bilgisi bulunamadi." });

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { error = "Kullanici bilgisi bulunamadi." });

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);

        // Class year: enrollment-date based, adjusted for frozen semesters
        var classYear = 1;
        if (activeProgram != null)
        {
            var now = DateTime.UtcNow;
            var currentAcademicYear = now.Month >= 9 ? now.Year : now.Year - 1;
            var enrollDate = activeProgram.EnrollmentDate;
            var enrollAcademicYear = enrollDate.Month >= 9 ? enrollDate.Year : enrollDate.Year - 1;
            var elapsedYears = currentAcademicYear - enrollAcademicYear + 1;
            var frozenYears = (int)Math.Floor(student.FrozenSemesterCount / 2.0);
            var maxYears = activeProgram.Program?.DegreeLevel == DegreeLevel.Associate ? 2 : 4;
            classYear = Math.Clamp(elapsedYears - frozenYears, 1, maxYears);
        }

        var ectsLimitBelow200 = await _systemConfig.GetIntAsync("Registration.EctsLimit.CgpaBelow200", 30);
        var ectsLimit200To249 = await _systemConfig.GetIntAsync("Registration.EctsLimit.Cgpa200To249", 42);
        var ectsLimitAbove250 = await _systemConfig.GetIntAsync("Registration.EctsLimit.CgpaAbove250", 48);
        var ectsLimit = student.Cgpa >= 2.50m
            ? ectsLimitAbove250
            : student.Cgpa >= 2.00m
                ? ectsLimit200To249
                : ectsLimitBelow200;

        // Advisor info
        string? advisorName = null;
        string? advisorTitle = null;
        if (student.Advisor != null)
        {
            var advisorUser = await _context.Users.FindAsync(student.Advisor.UserId);
            if (advisorUser != null)
            {
                advisorName = $"{advisorUser.FirstName} {advisorUser.LastName}";
                advisorTitle = student.Advisor.Title;
            }
        }

        var dto = new StudentInfoDto
        {
            Id = student.Id,
            StudentNumber = student.StudentNumber,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Cgpa = student.Cgpa,
            Sgpa = student.Sgpa,
            CompletedSemesters = student.CompletedSemesters,
            Status = student.Status.ToString(),
            IsScholarshipStudent = student.IsScholarshipStudent,
            HasFinancialBlock = student.HasFinancialBlock,
            ProgramName = activeProgram?.Program?.Name ?? string.Empty,
            DepartmentName = activeProgram?.Program?.Department?.Name ?? string.Empty,
            FacultyName = activeProgram?.Program?.Department?.Faculty?.Name ?? string.Empty,
            WithdrawalCount = student.WithdrawalCount,
            ProgramId = activeProgram?.ProgramId ?? 0,
            ClassYear = classYear,
            EctsLimit = ectsLimit,
            AdvisorName = advisorName,
            AdvisorTitle = advisorTitle
        };

        return Ok(dto);
    }

    /// <summary>
    /// GET api/student/curriculum-status — returns the student's curriculum progress.
    /// </summary>
    [HttpGet("curriculum-status")]
    [RequirePermission("menu.ders-kayit.view")]
    // Patch 2A: Permission kaldırıldı; data multi-role (sadece authenticated).
    public async Task<IActionResult> GetCurriculumStatus()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var studentId = await _context.Students
            .Where(s => s.UserId == userId)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();

        if (studentId == null)
            return NotFound(new { error = "Ogrenci bilgisi bulunamadi." });

        var dto = await _curriculumStatusService.GetCurriculumStatusAsync(studentId.Value);
        return dto == null
            ? NotFound(new { error = "Aktif program bulunamadi." })
            : Ok(dto);
    }

    /// <summary>
    /// GET api/student/term-gpa-history — returns the student's term-based ANO/AGNO timeline.
    /// </summary>
    [HttpGet("term-gpa-history")]
    [RequirePermission("menu.donem-ortalamalari.view")]
    public async Task<IActionResult> GetTermGpaHistory()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (student == null)
            return NotFound(new { error = "Ogrenci bilgisi bulunamadi." });

        var evaluationEnabled = await _systemConfig.GetBoolAsync("Application.SocialTranscriptApplicationEvaluationEnabled", true);
        if (!evaluationEnabled)
            return BadRequest(new { error = "Sosyal transkript değerlendirme akışı sistem konfigürasyonunda kapalı." });

        var evaluationAccess = await ResolveStudentModuleAccessAsync(student.Id, "social-transcript-application-evaluation");
        if (!evaluationAccess.IsOpen)
            return BadRequest(new { error = evaluationAccess.Message ?? "Sosyal transkript değerlendirme akışı şu anda kapalı." });

        var history = await _gpaCalculationService.GetTermGpaHistoryAsync(student.Id);
        return Ok(history);
    }

    /// <summary>
    /// GET api/student/registrations/{registrationId}/grade-statistics - returns exam and grade statistics for the student's own registration.
    /// </summary>
    [HttpGet("registrations/{registrationId:int}/grade-statistics")]
    [RequirePermission("menu.alinan-dersler.statistics")]
    public async Task<IActionResult> GetGradeStatistics(int registrationId, [FromQuery] bool commonEvaluation = false)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (student == null)
            return NotFound(new { error = "Ogrenci bilgisi bulunamadi." });

        var objectionEnabled = await _systemConfig.GetBoolAsync("Application.SocialTranscriptObjectionEnabled", true);
        if (!objectionEnabled)
            return BadRequest(new { error = "Sosyal transkript itiraz akışı sistem konfigürasyonunda kapalı." });

        var objectionAccess = await ResolveStudentModuleAccessAsync(student.Id, "social-transcript-student-objection");
        if (!objectionAccess.IsOpen)
            return BadRequest(new { error = objectionAccess.Message ?? "Sosyal transkript itiraz akışı şu anda kapalı." });

        var registration = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Instructor)
            .FirstOrDefaultAsync(cr => cr.Id == registrationId && cr.StudentId == student.Id);

        if (registration == null)
            return NotFound(new { error = "Kayit bulunamadi." });

        var section = registration.CourseSection;
        var course = section.Course;
        var term = section.Term;

        var components = await _context.GradeComponents
            .AsNoTracking()
            .Where(component => component.CourseId == section.CourseId && component.AcademicTermId == section.TermId)
            .OrderBy(component => component.Order)
            .ThenBy(component => component.Id)
            .ToListAsync();

        var sectionRegistrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(item => item.CourseSectionId == section.Id
                && item.Status != RegistrationStatus.Draft
                && item.Status != RegistrationStatus.Dropped)
            .ToListAsync();

        var statisticRegistrations = commonEvaluation
            ? await _context.CourseRegistrations
                .AsNoTracking()
                .Where(item => item.CourseSection.CourseId == section.CourseId
                    && item.CourseSection.TermId == section.TermId
                    && item.Status != RegistrationStatus.Draft
                    && item.Status != RegistrationStatus.Dropped)
                .ToListAsync()
            : sectionRegistrations;

        var registrationIds = statisticRegistrations.Select(item => item.Id).ToList();
        var allGrades = registrationIds.Count == 0
            ? new List<StudentGrade>()
            : await _context.StudentGrades
                .AsNoTracking()
                .Where(grade => registrationIds.Contains(grade.CourseRegistrationId))
                .ToListAsync();

        var gradesByRegistration = allGrades
            .GroupBy(grade => grade.CourseRegistrationId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var totalComponentWeight = components.Sum(component => component.Weight);

        decimal? CalculateWeightedAverage(int courseRegistrationId, bool requireAllComponentScores = true)
        {
            if (components.Count == 0 || !gradesByRegistration.TryGetValue(courseRegistrationId, out var studentGrades))
                return null;

            if (requireAllComponentScores)
            {
                var hasAllScores = totalComponentWeight == 100m
                    && components.All(component => studentGrades.Any(grade =>
                        grade.GradeComponentId == component.Id && grade.Score != null));

                if (!hasAllScores)
                    return null;
            }

            decimal totalWeightedScore = 0;
            decimal totalWeight = 0;

            foreach (var component in components)
            {
                var score = studentGrades
                    .FirstOrDefault(grade => grade.GradeComponentId == component.Id)
                    ?.Score;

                if (score == null)
                    continue;

                totalWeightedScore += score.Value * component.Weight / 100m;
                totalWeight += component.Weight;
            }

            return totalWeight > 0
                ? Math.Round(totalWeightedScore * 100m / totalWeight, 2, MidpointRounding.AwayFromZero)
                : null;
        }

        var weightedAverages = statisticRegistrations
            .Select(item => CalculateWeightedAverage(item.Id))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        var criteria = await _context.GradingCriteria
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.CourseId == section.CourseId && item.AcademicTermId == section.TermId);
        var hasGradingCriteria = criteria != null;

        var gradeCounts = statisticRegistrations
            .Where(item => item.Grade != null)
            .GroupBy(item => item.Grade!.Value.ToString())
            .ToDictionary(group => group.Key, group => group.Count());

        var instructorName = string.Empty;
        if (section.Instructor != null)
        {
            var instructorUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.Id == section.Instructor.UserId);

            if (instructorUser != null)
                instructorName = $"{section.Instructor.Title} {instructorUser.FirstName} {instructorUser.LastName}".Trim();
        }

        var activeProgram = student.StudentPrograms.FirstOrDefault(program => program.IsActive);
        var programName = activeProgram?.Program?.Name ?? string.Empty;
        var facultyName = activeProgram?.Program?.Department?.Faculty?.Name ?? string.Empty;
        var ownGrades = gradesByRegistration.GetValueOrDefault(registration.Id, new List<StudentGrade>());
        var componentVisibility = await BuildComponentVisibilityMapAsync(term.Id, components);
        var visibleComponents = components
            .Where(component => componentVisibility.GetValueOrDefault(component.Id, true))
            .ToList();
        var hasAnyComponentScore = visibleComponents.Any(component =>
            ownGrades.Any(grade => grade.GradeComponentId == component.Id && grade.Score != null));
        var hasAllComponentScores = visibleComponents.Count > 0
            && visibleComponents.All(component => ownGrades.Any(grade =>
                grade.GradeComponentId == component.Id && grade.Score != null));
        var hasFinalizedGrade = section.IsFinalized
            && hasGradingCriteria
            && registration.Grade != null
            && hasAllComponentScores;
        var calendarAllowsLetterGrade = await _calendarRuleService.CanStudentSeeLetterGradeAsync(
            section.TermId,
            hasFinalizedGrade,
            registration.Grade != null,
            DateTime.UtcNow);
        var moduleAllowsLetterGrade = await CanStudentSeeLetterGradeAsync(section.TermId);
        var isResultFinalized = calendarAllowsLetterGrade && moduleAllowsLetterGrade;
        var studentWeightedAverage = visibleComponents.Count == components.Count
            ? CalculateWeightedAverage(registration.Id)
            : null;

        var componentStatistics = components
            .Select(component =>
            {
                var componentScores = allGrades
                    .Where(grade => grade.GradeComponentId == component.Id && grade.Score != null)
                    .Select(grade => grade.Score!.Value)
                    .ToList();
                var studentScore = gradesByRegistration.TryGetValue(registration.Id, out var ownGrades)
                    ? ownGrades.FirstOrDefault(grade => grade.GradeComponentId == component.Id)?.Score
                    : null;
                var isVisible = componentVisibility.GetValueOrDefault(component.Id, true);

                return new StudentComponentStatisticsDto
                {
                    ComponentId = component.Id,
                    Name = component.Name,
                    Weight = component.Weight,
                    TotalStudentCount = statisticRegistrations.Count,
                    EnteredStudentCount = componentScores.Count,
                    MissingStudentCount = statisticRegistrations.Count - componentScores.Count,
                    AverageScore = componentScores.Count > 0
                        ? Math.Round(componentScores.Average(), 2, MidpointRounding.AwayFromZero)
                        : null,
                    StudentScore = isVisible ? studentScore : null
                };
            })
            .ToList();

        var dto = new StudentGradeStatisticsDto
        {
            RegistrationId = registration.Id,
            CourseSectionId = section.Id,
            TermName = term.Name,
            FacultyProgram = string.IsNullOrWhiteSpace(facultyName)
                ? programName
                : $"{facultyName} - {programName}",
            InstructorName = instructorName,
            CourseCode = course.Code,
            CourseName = course.Name,
            SectionNumber = section.SectionNumber,
            StudentLetterGrade = isResultFinalized ? registration.Grade?.ToString() : null,
            StudentWeightedAverage = studentWeightedAverage,
            HasGradingCriteria = hasGradingCriteria,
            HasAnyComponentScore = hasAnyComponentScore,
            HasAllComponentScores = hasAllComponentScores,
            IsResultFinalized = isResultFinalized,
            IsCommonEvaluation = commonEvaluation,
            Result = new StudentResultInfoDto
            {
                Status = isResultFinalized ? "Sonuçlandırılmış" : "Sonuçlandırılmamış",
                StatusDate = isResultFinalized ? section.UpdatedAt ?? registration.UpdatedAt : null,
                EvaluationMethod = hasGradingCriteria
                    ? commonEvaluation ? "Mutlak - Ortak Değerlendirme" : "Mutlak"
                    : string.Empty
            },
            GradeRanges = hasGradingCriteria
                ? BuildGradeRanges(criteria!, isResultFinalized ? gradeCounts : new Dictionary<string, int>())
                : new List<StudentGradeRangeDto>(),
            Components = componentStatistics,
            Summary = new StudentSectionStatisticsSummaryDto
            {
                TotalStudentCount = statisticRegistrations.Count,
                GradedStudentCount = isResultFinalized
                    ? statisticRegistrations.Count(item => item.Grade != null)
                    : 0,
                ClassAverage = weightedAverages.Count > 0
                    ? Math.Round(weightedAverages.Average(), 2, MidpointRounding.AwayFromZero)
                    : null
            }
        };

        return Ok(dto);
    }

    /// <summary>
    /// GET api/student/transcript — returns the authenticated student's transcript document data.
    /// </summary>
    [HttpGet("transcript")]
    [RequirePermission("menu.transkript.view")]
    public async Task<IActionResult> GetTranscript()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (student == null)
            return NotFound(new { error = "Ogrenci bilgisi bulunamadi." });

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound(new { error = "Kullanici bilgisi bulunamadi." });

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);
        if (activeProgram == null)
            return NotFound(new { error = "Aktif program bulunamadi." });

        var transcriptEnabled = await _systemConfig.GetBoolAsync("Application.SocialTranscriptStudentApplicationEnabled", true);
        if (!transcriptEnabled)
            return BadRequest(new { error = "Sosyal transkript başvuru akışı sistem konfigürasyonunda kapalı." });

        var transcriptAccess = await ResolveStudentModuleAccessAsync(student.Id, "social-transcript-student-application");
        if (!transcriptAccess.IsOpen)
            return BadRequest(new { error = transcriptAccess.Message ?? "Sosyal transkript başvuru akışı şu anda kapalı." });

        var transcript = await _transcriptService.GetTranscriptAsync(student.Id);
        if (transcript == null)
            return NotFound(new { error = "Transkript bilgisi bulunamadi." });

        return Ok(transcript);
    }

    private async Task<Dictionary<int, bool>> BuildComponentVisibilityMapAsync(int termId, IEnumerable<GradeComponent> components)
    {
        var result = new Dictionary<int, bool>();

        foreach (var component in components)
        {
            var controlCode = ResolvePublicationControlCode(component.Name);
            var access = await _webModuleControls.EvaluateAsync(controlCode, termId);
            result[component.Id] = access.IsOpen;
        }

        return result;
    }

    private async Task<bool> CanStudentSeeLetterGradeAsync(int termId)
    {
        var finalAccess = await _webModuleControls.EvaluateAsync("letter-grade-publication", termId);
        if (finalAccess.IsOpen)
            return true;

        var makeupAccess = await _webModuleControls.EvaluateAsync("makeup-letter-grade-publication", termId);
        return makeupAccess.IsOpen;
    }

    private async Task<WebModuleAccessResult> ResolveStudentModuleAccessAsync(int studentId, string controlCode)
    {
        var activeTermId = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(registration => registration.StudentId == studentId
                && registration.CourseSection.TermId == registration.CourseSection.Term.Id
                && registration.Status != RegistrationStatus.Draft
                && registration.Status != RegistrationStatus.Dropped)
            .OrderByDescending(registration => registration.CourseSection.Term.TermStart)
            .Select(registration => (int?)registration.CourseSection.TermId)
            .FirstOrDefaultAsync();

        if (!activeTermId.HasValue)
        {
            activeTermId = await _context.AcademicTerms
                .AsNoTracking()
                .ActiveTerm()
                .Select(term => (int?)term.Id)
                .FirstOrDefaultAsync();
        }

        return await _webModuleControls.EvaluateAsync(controlCode, activeTermId);
    }

    private static string ResolvePublicationControlCode(string componentName)
    {
        var normalized = componentName.Trim().ToLowerInvariant();

        if (normalized.Contains("mazeret") || normalized.Contains("excuse"))
        {
            return normalized.Contains("ara") || normalized.Contains("midterm") || normalized.Contains("vize")
                ? "midterm-excuse-grade-publication"
                : "makeup-grade-publication";
        }

        if (normalized.Contains("ara") || normalized.Contains("midterm") || normalized.Contains("vize"))
            return "midterm-grade-publication";

        if (normalized.Contains("bütünleme") || normalized.Contains("butunleme") || normalized.Contains("makeup") || normalized.Contains("yys"))
            return "makeup-grade-publication";

        if (normalized.Contains("yarıyıl") || normalized.Contains("yariyil") || normalized.Contains("final"))
            return "final-grade-publication";

        return "letter-grade-publication";
    }

    private static List<StudentGradeRangeDto> BuildGradeRanges(
        GradingCriteria criteria,
        IReadOnlyDictionary<string, int> gradeCounts)
    {
        static StudentGradeRangeDto Range(
            string grade,
            decimal min,
            decimal max,
            IReadOnlyDictionary<string, int> gradeCounts) => new()
            {
                Grade = grade,
                MinScore = min,
                MaxScore = max,
                StudentCount = gradeCounts.GetValueOrDefault(grade)
            };

        return
        [
            Range("AA", criteria.AAMin, 100m, gradeCounts),
            Range("BA", criteria.BAMin, criteria.AAMin - 0.01m, gradeCounts),
            Range("BB", criteria.BBMin, criteria.BAMin - 0.01m, gradeCounts),
            Range("CB", criteria.CBMin, criteria.BBMin - 0.01m, gradeCounts),
            Range("CC", criteria.CCMin, criteria.CBMin - 0.01m, gradeCounts),
            Range("DC", criteria.DCMin, criteria.CCMin - 0.01m, gradeCounts),
            Range("DD", criteria.DDMin, criteria.DCMin - 0.01m, gradeCounts),
            Range("FF", 0m, criteria.DDMin - 0.01m, gradeCounts)
        ];
    }
}
