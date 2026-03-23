using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicLevel.DTOs;
using SIS.Application.AcademicLevel.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AcademicLevelService : IAcademicLevelService
{
    private readonly AppDbContext _context;
    private readonly IGpaCalculationService _gpaCalculationService;

    // Zorunlu ders tamamlanma esigi: bu oran karsilanirsa o yil "tamamlanmis" sayilir
    private const decimal YearCompletionThreshold = 0.60m;

    private static readonly string[] ClassLabels = { "Hazirlik", "1. Sinif", "2. Sinif", "3. Sinif", "4. Sinif", "5. Sinif", "6. Sinif" };

    public AcademicLevelService(AppDbContext context, IGpaCalculationService gpaCalculationService)
    {
        _context = context;
        _gpaCalculationService = gpaCalculationService;
    }

    public async Task<AcademicLevelDto?> GetStudentAcademicLevelAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        if (student == null)
            return null;

        var mainProgram = student.StudentPrograms
            .FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);

        if (mainProgram == null)
            return null;

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == student.UserId, cancellationToken);

        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(r => r.CourseSection)
                .ThenInclude(s => s.Course)
            .Where(r => r.StudentId == studentId)
            .ToListAsync(cancellationToken);

        var curriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => cc.CurriculumId == mainProgram.CurriculumId)
            .ToListAsync(cancellationToken);

        return BuildAcademicLevel(student, mainProgram, user, registrations, curriculumCourses);
    }

    public async Task<BulkAcademicLevelDto> GetDepartmentAcademicLevelsAsync(
        int departmentId,
        CancellationToken cancellationToken = default)
    {
        var students = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .Where(s => s.Status == StudentStatus.Active
                && s.StudentPrograms.Any(sp =>
                    sp.Type == StudentProgramType.Main
                    && sp.IsActive
                    && sp.Program.DepartmentId == departmentId))
            .ToListAsync(cancellationToken);

        if (students.Count == 0)
            return new BulkAcademicLevelDto();

        var studentIds = students.Select(s => s.Id).ToList();
        var userIds = students.Select(s => s.UserId).ToList();

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(r => r.CourseSection)
                .ThenInclude(s => s.Course)
            .Where(r => studentIds.Contains(r.StudentId))
            .ToListAsync(cancellationToken);

        var registrationsByStudent = registrations
            .GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var curriculumIds = students
            .SelectMany(s => s.StudentPrograms)
            .Where(sp => sp.Type == StudentProgramType.Main && sp.IsActive)
            .Select(sp => sp.CurriculumId)
            .Distinct()
            .ToList();

        var allCurriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => curriculumIds.Contains(cc.CurriculumId))
            .ToListAsync(cancellationToken);

        var curriculumCoursesByCurriculum = allCurriculumCourses
            .GroupBy(cc => cc.CurriculumId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<AcademicLevelDto>();

        foreach (var student in students)
        {
            var mainProgram = student.StudentPrograms
                .FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);

            if (mainProgram == null) continue;

            var studentRegs = registrationsByStudent
                .GetValueOrDefault(student.Id, new List<CourseRegistration>());

            var ccList = curriculumCoursesByCurriculum
                .GetValueOrDefault(mainProgram.CurriculumId, new List<CurriculumCourse>());

            var user = users.GetValueOrDefault(student.UserId);
            var dto = BuildAcademicLevel(student, mainProgram, user, studentRegs, ccList);
            results.Add(dto);
        }

        var bulk = new BulkAcademicLevelDto
        {
            TotalStudents = results.Count,
            DiscrepancyCount = results.Count(r => r.HasDiscrepancy),
            Students = results.OrderBy(r => r.StudentNumber).ToList()
        };

        bulk.OfficialDistribution = BuildDistribution(results, r => r.OfficialClassLevel);
        bulk.AcademicDistribution = BuildDistribution(results, r => r.AcademicLevel);

        return bulk;
    }

    private AcademicLevelDto BuildAcademicLevel(
        Student student,
        StudentProgram mainProgram,
        ApplicationUser? user,
        List<CourseRegistration> registrations,
        List<CurriculumCourse> curriculumCourses)
    {
        var program = mainProgram.Program;
        var totalRequiredEcts = program?.TotalRequiredEcts ?? 240;
        var maxDurationSemesters = program?.MaxStudyDurationSemesters ?? 14;
        var normalDurationYears = GetNormalDurationYears(program);

        // Gecilen ders ID'leri
        var passedCourseIds = registrations
            .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
            .Select(r => r.CourseSection.CourseId)
            .ToHashSet();

        // Tamamlanan ECTS
        var passedCourseEcts = registrations
            .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
            .GroupBy(r => r.CourseSection.CourseId)
            .Sum(g => g.First().CourseSection.Course?.Ects ?? 0);

        // --- Official Class Level (kayit yili bazli) ---
        var enrollmentDate = mainProgram.EnrollmentDate;
        var currentAcademicYear = GetCurrentAcademicYear();
        var enrollmentAcademicYear = GetAcademicYear(enrollmentDate);
        var elapsedYears = currentAcademicYear - enrollmentAcademicYear + 1;
        var frozenYears = (int)Math.Floor(student.FrozenSemesterCount / 2.0);
        var officialLevel = Math.Clamp(elapsedYears - frozenYears, 1, normalDurationYears);

        // --- Academic Level (ilerleme bazli) ---
        var ectsPerYear = totalRequiredEcts > 0 && normalDurationYears > 0
            ? (decimal)totalRequiredEcts / normalDurationYears
            : 60m;

        var ectsBasedLevel = ectsPerYear > 0
            ? (int)Math.Floor(passedCourseEcts / ectsPerYear) + 1
            : 1;

        // Yil bazli mufredat tamamlanma analizi
        var yearProgress = new List<YearProgressDto>();
        var curriculumLevel = 1;

        for (var year = 1; year <= normalDurationYears; year++)
        {
            var semesterStart = (year - 1) * 2 + 1;
            var semesterEnd = year * 2;

            var yearCourses = curriculumCourses
                .Where(cc => cc.SuggestedSemester >= semesterStart
                    && cc.SuggestedSemester <= semesterEnd
                    && cc.IsCompulsory)
                .ToList();

            var completedCount = yearCourses.Count(cc => passedCourseIds.Contains(cc.CourseId));
            var totalYearEcts = yearCourses.Sum(cc =>
            {
                // CurriculumCourse doesn't have Ects directly; look up from passed registrations
                var reg = registrations.FirstOrDefault(r =>
                    r.CourseSection.CourseId == cc.CourseId
                    && RegistrationRulePolicy.IsPassedGrade(r.Grade));
                return reg?.CourseSection.Course?.Ects ?? 0;
            });

            // Total ECTS for all courses in this year (whether passed or not)
            // We need the actual course ECTS, not just passed ones
            var allYearEcts = 0;
            foreach (var cc in yearCourses)
            {
                var courseEcts = registrations
                    .FirstOrDefault(r => r.CourseSection.CourseId == cc.CourseId)
                    ?.CourseSection.Course?.Ects ?? 0;
                allYearEcts += courseEcts;
            }

            var completionRate = yearCourses.Count > 0
                ? (decimal)completedCount / yearCourses.Count
                : 0m;

            yearProgress.Add(new YearProgressDto
            {
                Year = year,
                YearLabel = GetClassLabel(year),
                TotalMandatoryCourses = yearCourses.Count,
                CompletedMandatoryCourses = completedCount,
                CompletionRate = Math.Round(completionRate * 100, 1),
                TotalEcts = allYearEcts > 0 ? allYearEcts : (int)(ectsPerYear),
                CompletedEcts = totalYearEcts
            });

            if (completionRate >= YearCompletionThreshold)
                curriculumLevel = year + 1;
        }

        // Bilesik akademik seviye: ECTS ve mufredat bazli seviyelerin minimumu
        var academicLevel = Math.Clamp(
            Math.Min(ectsBasedLevel, curriculumLevel),
            1,
            normalDurationYears);

        var ectsCompletionRate = totalRequiredEcts > 0
            ? Math.Round((decimal)passedCourseEcts / totalRequiredEcts * 100, 1)
            : 0m;

        var hasDiscrepancy = officialLevel != academicLevel;

        return new AcademicLevelDto
        {
            StudentId = student.Id,
            StudentNumber = student.StudentNumber,
            FullName = user != null ? $"{user.FirstName} {user.LastName}" : student.StudentNumber,
            OfficialClassLevel = officialLevel,
            OfficialClassLabel = GetClassLabel(officialLevel),
            AcademicLevel = academicLevel,
            AcademicLevelLabel = GetClassLabel(academicLevel),
            CompletedEcts = passedCourseEcts,
            TotalRequiredEcts = totalRequiredEcts,
            EctsCompletionRate = ectsCompletionRate,
            CompletedSemesters = student.CompletedSemesters,
            FrozenSemesters = student.FrozenSemesterCount,
            YearProgress = yearProgress,
            HasDiscrepancy = hasDiscrepancy,
            DiscrepancyNote = hasDiscrepancy
                ? $"Resmi sinif seviyesi ({GetClassLabel(officialLevel)}) ile akademik seviye ({GetClassLabel(academicLevel)}) uyusmamaktadir. " +
                  $"Akademik seviye, tamamlanan AKTS ({passedCourseEcts}/{totalRequiredEcts}) ve mufredat ilerlemesine gore hesaplandi."
                : null
        };
    }

    private static int GetNormalDurationYears(AcademicProgram? program)
    {
        if (program == null) return 4;

        return program.DegreeLevel switch
        {
            DegreeLevel.Associate => 2,
            DegreeLevel.Bachelor => 4,
            DegreeLevel.Master => 2,
            DegreeLevel.Doctorate => 4,
            _ => 4
        };
    }

    private static int GetCurrentAcademicYear()
    {
        var now = DateTime.UtcNow;
        return now.Month >= 9 ? now.Year : now.Year - 1;
    }

    private static int GetAcademicYear(DateTime date)
    {
        return date.Month >= 9 ? date.Year : date.Year - 1;
    }

    private static string GetClassLabel(int level)
    {
        if (level <= 0) return "Belirsiz";
        if (level < ClassLabels.Length) return ClassLabels[level];
        return $"{level}. Sinif";
    }

    private static List<LevelDistributionDto> BuildDistribution(
        List<AcademicLevelDto> results,
        Func<AcademicLevelDto, int> levelSelector)
    {
        var total = results.Count;
        return results
            .GroupBy(levelSelector)
            .OrderBy(g => g.Key)
            .Select(g => new LevelDistributionDto
            {
                Level = g.Key,
                Label = GetClassLabel(g.Key),
                Count = g.Count(),
                Percentage = total > 0 ? Math.Round((decimal)g.Count() / total * 100, 1) : 0
            })
            .ToList();
    }
}
