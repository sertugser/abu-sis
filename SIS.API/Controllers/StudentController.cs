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

    public StudentController(
        AppDbContext context,
        IGpaCalculationService gpaCalculationService,
        IAcademicCalendarRuleService calendarRuleService,
        ISystemConfigProvider systemConfig,
        ICurriculumStatusService curriculumStatusService,
        IWebModuleControlService? webModuleControls = null)
    {
        _context = context;
        _gpaCalculationService = gpaCalculationService;
        _calendarRuleService = calendarRuleService;
        _systemConfig = systemConfig;
        _curriculumStatusService = curriculumStatusService;
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

        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Where(cr => cr.StudentId == student.Id
                && cr.Grade != null
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped)
            .ToListAsync();

        var transcriptGradeVisibility = await BuildGradeVisibilityMapAsync(registrations.Select(r => r.CourseSection.TermId));
        registrations = registrations
            .Where(registration => transcriptGradeVisibility.GetValueOrDefault(registration.CourseSection.TermId, true))
            .ToList();

        var registrationIds = registrations.Select(registration => registration.Id).ToList();
        var courseIds = registrations
            .Select(registration => registration.CourseSection.CourseId)
            .Distinct()
            .ToList();
        var termIds = registrations
            .Select(registration => registration.CourseSection.TermId)
            .Distinct()
            .ToList();
        var equivalentCourseIdsByCourseId = courseIds.Count == 0
            ? new Dictionary<int, HashSet<int>>()
            : (await _context.CourseEquivalencies
                .AsNoTracking()
                .Where(equivalency => courseIds.Contains(equivalency.CourseId))
                .ToListAsync())
                .GroupBy(equivalency => equivalency.CourseId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(equivalency => equivalency.EquivalentCourseId).ToHashSet());
        var courseChainIdByCourseId = BuildCourseChainIdMap(courseIds, equivalentCourseIdsByCourseId);
        var registrationMetadataById = registrations.ToDictionary(
            registration => registration.Id,
            registration => (registration.CourseSection.CourseId, registration.CourseSection.TermId));
        var gradeComponentStatsByCourseTerm = courseIds.Count == 0 || termIds.Count == 0
            ? new Dictionary<(int CourseId, int TermId), (decimal TotalWeight, int ComponentCount)>()
            : (await _context.GradeComponents
                .AsNoTracking()
                .Where(component => courseIds.Contains(component.CourseId) && termIds.Contains(component.AcademicTermId))
                .Select(component => new
                {
                    component.CourseId,
                    TermId = component.AcademicTermId,
                    component.Id,
                    component.Weight
                })
                .ToListAsync())
                .GroupBy(component => (component.CourseId, component.TermId))
                .ToDictionary(
                    group => group.Key,
                    group => (
                        TotalWeight: group.Sum(component => component.Weight),
                        ComponentCount: group.Select(component => component.Id).Distinct().Count()));
        var weightedAverageMap = registrationIds.Count == 0
            ? new Dictionary<int, decimal?>()
            : (await _context.StudentGrades
                .AsNoTracking()
                .Include(grade => grade.GradeComponent)
                .Where(grade => registrationIds.Contains(grade.CourseRegistrationId) && grade.Score != null)
                .ToListAsync())
                .GroupBy(grade => grade.CourseRegistrationId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        if (!registrationMetadataById.TryGetValue(group.Key, out var registrationMetadata))
                            return (decimal?)null;

                        if (!gradeComponentStatsByCourseTerm.TryGetValue(
                                (registrationMetadata.CourseId, registrationMetadata.TermId),
                                out var componentStats)
                            || componentStats.TotalWeight <= 0
                            || componentStats.ComponentCount <= 0)
                            return (decimal?)null;

                        var gradedComponents = group
                            .Where(item => item.GradeComponent != null)
                            .GroupBy(item => item.GradeComponentId)
                            .Select(componentGroup =>
                            {
                                var grade = componentGroup.First();
                                return new
                                {
                                    Score = grade.Score!.Value,
                                    grade.GradeComponent.Weight
                                };
                            })
                            .ToList();

                        var actualWeight = gradedComponents.Sum(item => item.Weight);
                        if (gradedComponents.Count != componentStats.ComponentCount || actualWeight != componentStats.TotalWeight)
                            return (decimal?)null;

                        var totalWeightedScore = gradedComponents.Sum(item => item.Score * item.Weight / 100m);
                        return Math.Round(
                            totalWeightedScore * 100m / componentStats.TotalWeight,
                            2,
                            MidpointRounding.AwayFromZero);
                    });

        var repeatedChainCounts = registrations
            .GroupBy(registration => courseChainIdByCourseId.GetValueOrDefault(registration.CourseSection.CourseId, registration.CourseSection.CourseId))
            .ToDictionary(group => group.Key, group => group.Count());
        var latestGradesByCourse = new Dictionary<int, CourseRegistration>();
        var termGroups = registrations
            .GroupBy(cr => cr.CourseSection.TermId)
            .Select(group => new
            {
                Term = group.First().CourseSection.Term,
                Registrations = group
                    .OrderBy(cr => cr.CourseSection.Course.Code)
                    .ThenBy(cr => cr.Id)
                    .ToList()
            })
            .OrderBy(group => group.Term.TermStart)
            .ThenBy(group => group.Term.Id)
            .ToList();

        var transcriptTerms = new List<StudentTranscriptTermDto>(termGroups.Count);

        foreach (var termGroup in termGroups)
        {
            foreach (var registration in termGroup.Registrations
                .OrderBy(registration => registration.Id))
            {
                var chainId = courseChainIdByCourseId.GetValueOrDefault(
                    registration.CourseSection.CourseId,
                    registration.CourseSection.CourseId);
                latestGradesByCourse[chainId] = registration;
            }

            var courseDtos = termGroup.Registrations
                .Select(registration =>
                {
                    var course = registration.CourseSection.Course;
                    var chainId = courseChainIdByCourseId.GetValueOrDefault(course.Id, course.Id);
                    var credits = course.TheoryHours + course.LabHours;
                    var grade = registration.Grade!.Value;
                    var gradePoint = GradePointMap.GetPoint(grade);
                    var weightedEcts = gradePoint.HasValue
                        ? Math.Round(course.Ects * gradePoint.Value, 2, MidpointRounding.AwayFromZero)
                        : (decimal?)null;

                    return new StudentTranscriptCourseDto
                    {
                        RegistrationId = registration.Id,
                        CourseId = course.Id,
                        CourseCode = course.Code,
                        CourseName = course.Name,
                        Credits = credits,
                        Ects = course.Ects,
                        EquivalentCourseIds = equivalentCourseIdsByCourseId.TryGetValue(course.Id, out var equivalentIds)
                            ? equivalentIds.OrderBy(id => id).ToList()
                            : new List<int>(),
                        AverageScore = weightedAverageMap.GetValueOrDefault(registration.Id),
                        Grade = grade.ToString(),
                        Status = registration.Status.ToString(),
                        IsRepeated = registration.IsRepeated || repeatedChainCounts.GetValueOrDefault(chainId) > 1,
                        IsPassed = IsPassedGrade(grade),
                        CountsTowardsGpa = gradePoint.HasValue,
                        GradePoint = gradePoint,
                        WeightedEcts = weightedEcts
                    };
                })
                .ToList();

            var attemptedCredits = courseDtos.Sum(course => course.Credits);
            var attemptedEcts = courseDtos.Sum(course => course.Ects);
            var earnedCredits = courseDtos.Where(course => course.IsPassed).Sum(course => course.Credits);
            var earnedEcts = courseDtos.Where(course => course.IsPassed).Sum(course => course.Ects);
            var gpaEcts = courseDtos.Where(course => course.CountsTowardsGpa).Sum(course => course.Ects);
            var gpaPoints = courseDtos.Sum(course => course.WeightedEcts ?? 0m);

            var cumulativeRegistrations = latestGradesByCourse.Values.ToList();
            var cumulativeGpaEcts = cumulativeRegistrations.Sum(GetGpaEcts);
            var cumulativeGpaPoints = cumulativeRegistrations.Sum(GetWeightedEcts);

            transcriptTerms.Add(new StudentTranscriptTermDto
            {
                TermId = termGroup.Term.Id,
                TermName = termGroup.Term.Name,
                Year = termGroup.Term.Year,
                Semester = termGroup.Term.TermType.ToString(),
                AttemptedCredits = attemptedCredits,
                AttemptedEcts = attemptedEcts,
                EarnedCredits = earnedCredits,
                EarnedEcts = earnedEcts,
                GpaEcts = gpaEcts,
                GpaPoints = Math.Round(gpaPoints, 2, MidpointRounding.AwayFromZero),
                CumulativeGpaEcts = cumulativeGpaEcts,
                CumulativeGpaPoints = Math.Round(cumulativeGpaPoints, 2, MidpointRounding.AwayFromZero),
                Sgpa = CalculateAverage(termGroup.Registrations),
                Cgpa = CalculateAverage(cumulativeRegistrations),
                Courses = courseDtos
            });
        }

        var latestRegistrations = latestGradesByCourse.Values.ToList();
        var totalGpaEcts = latestRegistrations.Sum(GetGpaEcts);
        var totalGpaPoints = latestRegistrations.Sum(GetWeightedEcts);

        var transcript = new StudentTranscriptDto
        {
            StudentId = student.Id,
            StudentNumber = student.StudentNumber,
            FullName = $"{user.FirstName} {user.LastName}",
            FacultyName = activeProgram.Program?.Department?.Faculty?.Name ?? string.Empty,
            DepartmentName = activeProgram.Program?.Department?.Name ?? string.Empty,
            ProgramName = activeProgram.Program?.Name ?? string.Empty,
            StudentStatus = student.Status.ToString(),
            EnrollmentDate = activeProgram.EnrollmentDate,
            GeneratedAt = DateTime.UtcNow,
            CurrentCgpa = transcriptTerms.Count > 0 ? transcriptTerms[^1].Cgpa : student.Cgpa,
            TotalAttemptedCredits = latestRegistrations.Sum(reg => reg.CourseSection.Course.TheoryHours + reg.CourseSection.Course.LabHours),
            TotalAttemptedEcts = latestRegistrations.Sum(reg => reg.CourseSection.Course.Ects),
            TotalEarnedCredits = latestRegistrations.Where(reg => IsPassedGrade(reg.Grade!.Value)).Sum(reg => reg.CourseSection.Course.TheoryHours + reg.CourseSection.Course.LabHours),
            TotalEarnedEcts = latestRegistrations.Where(reg => IsPassedGrade(reg.Grade!.Value)).Sum(reg => reg.CourseSection.Course.Ects),
            TotalGpaEcts = totalGpaEcts,
            TotalGpaPoints = Math.Round(totalGpaPoints, 2, MidpointRounding.AwayFromZero),
            Terms = transcriptTerms
        };

        return Ok(transcript);
    }

    private async Task<Dictionary<int, bool>> BuildGradeVisibilityMapAsync(IEnumerable<int> termIds)
    {
        var result = new Dictionary<int, bool>();
        foreach (var termId in termIds.Distinct())
        {
            result[termId] = await CanStudentSeeLetterGradeAsync(termId);
        }

        return result;
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

    private static bool IsPassedGrade(GradeCode grade) =>
        grade != GradeCode.FF
        && grade != GradeCode.NA
        && grade != GradeCode.U
        && grade != GradeCode.W
        && grade != GradeCode.I;

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

    private static int GetGpaEcts(CourseRegistration registration) =>
        GradePointMap.IsGpaIncluded(registration.Grade!.Value)
            ? registration.CourseSection.Course.Ects
            : 0;

    private static decimal GetWeightedEcts(CourseRegistration registration)
    {
        var point = GradePointMap.GetPoint(registration.Grade!.Value);
        return point.HasValue
            ? Math.Round(registration.CourseSection.Course.Ects * point.Value, 2, MidpointRounding.AwayFromZero)
            : 0m;
    }

    private static decimal CalculateAverage(IEnumerable<CourseRegistration> registrations)
    {
        var totalEcts = 0;
        decimal totalPoints = 0;

        foreach (var registration in registrations)
        {
            var point = GradePointMap.GetPoint(registration.Grade!.Value);
            if (!point.HasValue)
                continue;

            var ects = registration.CourseSection.Course.Ects;
            if (ects == 0)
                continue;

            totalEcts += ects;
            totalPoints += ects * point.Value;
        }

        if (totalEcts == 0)
            return 0m;

        return Math.Round(totalPoints / totalEcts, 2, MidpointRounding.AwayFromZero);
    }

    private static Dictionary<int, int> BuildCourseChainIdMap(
        IEnumerable<int> courseIds,
        IReadOnlyDictionary<int, HashSet<int>> equivalentCourseIdsByCourseId)
    {
        var distinctCourseIds = courseIds.Distinct().ToList();
        var map = distinctCourseIds.ToDictionary(courseId => courseId, courseId => courseId);
        var relevantCourseIds = distinctCourseIds.ToHashSet();
        var visited = new HashSet<int>();

        foreach (var courseId in distinctCourseIds)
        {
            if (!visited.Add(courseId))
                continue;

            var queue = new Queue<int>();
            var component = new List<int>();
            queue.Enqueue(courseId);

            while (queue.Count > 0)
            {
                var currentCourseId = queue.Dequeue();
                component.Add(currentCourseId);

                if (!equivalentCourseIdsByCourseId.TryGetValue(currentCourseId, out var equivalentIds))
                    continue;

                foreach (var equivalentCourseId in equivalentIds)
                {
                    if (!relevantCourseIds.Contains(equivalentCourseId) || !visited.Add(equivalentCourseId))
                        continue;

                    queue.Enqueue(equivalentCourseId);
                }
            }

            var chainId = component.Min();
            foreach (var componentCourseId in component)
            {
                map[componentCourseId] = chainId;
            }
        }

        return map;
    }
}
