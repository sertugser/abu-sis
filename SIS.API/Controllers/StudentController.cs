using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.CalendarGenerator.Services;
using SIS.Application.Common.Services;
using SIS.Application.Common.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.StudentCard.Services;
using SIS.Application.StudentCard.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using CurriculumEntity = SIS.Domain.Entities.Curriculum;

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
    private readonly IEctsLimitResolver _ectsLimitResolver;
    private readonly ITranscriptScenarioService _transcriptScenarioService;

    public StudentController(
        AppDbContext context,
        IGpaCalculationService gpaCalculationService,
        IAcademicCalendarRuleService calendarRuleService,
        ISystemConfigProvider systemConfig,
        ICurriculumStatusService curriculumStatusService,
        IStudentTranscriptService transcriptService,
        IEctsLimitResolver ectsLimitResolver,
        ITranscriptScenarioService transcriptScenarioService,
        IWebModuleControlService? webModuleControls = null)
    {
        _context = context;
        _gpaCalculationService = gpaCalculationService;
        _calendarRuleService = calendarRuleService;
        _systemConfig = systemConfig;
        _curriculumStatusService = curriculumStatusService;
        _transcriptService = transcriptService;
        _ectsLimitResolver = ectsLimitResolver;
        _transcriptScenarioService = transcriptScenarioService;
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

        var mainProgram = student.StudentPrograms.FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);
        CurriculumEntity? ectsCurriculum = mainProgram == null ? null
            : await _context.Curricula.AsNoTracking().FirstOrDefaultAsync(c => c.Id == mainProgram.CurriculumId, HttpContext.RequestAborted);
        var ectsLimit = await _ectsLimitResolver.ResolveAsync(student.Cgpa, ectsCurriculum, HttpContext.RequestAborted);

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

        // Not: Bu uç "Dersin Ayrıntıları" (Alınan Dersler) ve "Not Listesi" sayfaları
        // tarafından genel istatistik görünümü olarak kullanılır; sosyal transkript itiraz
        // akışına bağlı değildir. Yayın görünürlüğü zaten aşağıda bileşen bazında
        // (BuildComponentVisibilityMapAsync) ve harf notu için takvim kuralıyla uygulanır.
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
                var componentGrades = allGrades
                    .Where(grade => grade.GradeComponentId == component.Id)
                    .ToList();
                var componentScores = componentGrades
                    .Where(grade => grade.Score != null)
                    .Select(grade => grade.Score!.Value)
                    .ToList();
                var absentCount = componentGrades
                    .Count(grade => grade.ExamAttendance == ExamAttendance.Absent);
                var ownGrade = gradesByRegistration.TryGetValue(registration.Id, out var ownGradesLocal)
                    ? ownGradesLocal.FirstOrDefault(grade => grade.GradeComponentId == component.Id)
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
                    AbsentByAttendanceCount = absentCount,
                    AverageScore = componentScores.Count > 0
                        ? Math.Round(componentScores.Average(), 2, MidpointRounding.AwayFromZero)
                        : null,
                    StudentScore = isVisible ? ownGrade?.Score : null,
                    PublishedAt = isVisible ? component.PublishedAt : null,
                    StudentExamAttendance = isVisible
                        ? (ownGrade?.ExamAttendance ?? ExamAttendance.NotMarked).ToString()
                        : ExamAttendance.NotMarked.ToString()
                };
            })
            .ToList();

        // Bütünleme harf dağılımı: aynı GradingCriteria aralıkları (karar 2026-05-30),
        // sadece bütünleme notuyla nihai harfe ulaşmış öğrencileri sayar.
        // "Bütünlemeye giren" = bu section'ın herhangi bir bileşeninde MakeupScore != null.
        var makeupRegistrationIds = allGrades
            .Where(grade => grade.MakeupScore != null)
            .Select(grade => grade.CourseRegistrationId)
            .ToHashSet();
        var makeupGradeCounts = statisticRegistrations
            .Where(item => item.Grade != null && makeupRegistrationIds.Contains(item.Id))
            .GroupBy(item => item.Grade!.Value.ToString())
            .ToDictionary(group => group.Key, group => group.Count());

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
            MakeupGradeRanges = hasGradingCriteria
                ? BuildGradeRanges(criteria!, isResultFinalized ? makeupGradeCounts : new Dictionary<string, int>())
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

        var studentId = await _context.Students
            .Where(s => s.UserId == userId)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();

        if (studentId == null)
            return NotFound(new { error = "Ogrenci bilgisi bulunamadi." });

        // Öğrenci kendi transkriptini her zaman görüntüleyebilir (yetki: menu.transkript.view).
        // Yalnızca aktif dönemin henüz ilan edilmemiş notları gizlenir; bu kural
        // GetTranscriptAsync içindeki dönem-bazlı görünürlük mantığıyla uygulanır.
        var transcript = await _transcriptService.GetTranscriptAsync(studentId.Value);
        return transcript == null
            ? NotFound(new { error = "Transkript bilgisi bulunamadi." })
            : Ok(transcript);
    }

    /// <summary>
    /// POST api/student/transcript/scenario — Transkript Senaryosu (what-if) hesabı.
    /// Öğrencinin ekranda simüle ettiği ders listesini alır, AGNO/ANO/AKTS'yi TEK kaynaktan
    /// (GradePointMap + GpaMath kuralları) hesaplayıp döner. Sistemde HİÇBİR kalıcı değişiklik
    /// yapılmaz — salt bilgilendirme. Hesap backend'de yapılır; frontend kopya hesap tutmaz.
    /// </summary>
    [HttpPost("transcript/scenario")]
    [RequirePermission("menu.transkript-senaryosu.view")]
    public IActionResult CalculateTranscriptScenario([FromBody] TranscriptScenarioRequestDto request)
    {
        if (request == null)
            return BadRequest(new { error = "Senaryo verisi gönderilmedi." });

        return Ok(_transcriptScenarioService.Calculate(request));
    }

    private async Task<Dictionary<int, bool>> BuildComponentVisibilityMapAsync(int termId, IEnumerable<GradeComponent> components)
    {
        // Tek-kaynak görünürlük kapısı (karar 2026-05-30):
        //   (1) term-seviyesi admin: IWebModuleControlService.EvaluateAsync(...).IsOpen
        //   (2) komponent-seviyesi hoca: GradeComponent.PublishedAt != null && <= now
        // İkisi de gerekli — sadece biri açık değilse öğrenci notu/istatistiği göremez.
        // Bu sayede "admin açtı ama hoca ilan etmedi" ve tersi durumlar net ayrışır.
        var now = DateTime.UtcNow;
        var result = new Dictionary<int, bool>();

        foreach (var component in components)
        {
            var controlCode = SIS.Application.Grading.GradeComponentPublicationCode.Resolve(component.Name);
            var access = await _webModuleControls.EvaluateAsync(controlCode, termId);
            var publishedByInstructor = component.PublishedAt.HasValue && component.PublishedAt.Value <= now;
            result[component.Id] = access.IsOpen && publishedByInstructor;
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

    // Komponent → yayın kontrol kodu eşleme mantığı SIS.Application.Grading.GradeComponentPublicationCode
    // içinde tek-kaynak. Burada referans tutmuyoruz; doğrudan oradan çağrılır (yukarıda
    // BuildComponentVisibilityMapAsync). İkibaşlılık önlenir.

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
