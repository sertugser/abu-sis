using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIS.Application.EnrollmentForecast.DTOs;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class EnrollmentForecastService : IEnrollmentForecastService, IEnrollmentForecastCacheInvalidator
{
    private const string ForecastContextCacheKey = "ForecastContext";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _context;
    private readonly IGpaCalculationService _gpaCalculationService;
    private readonly IMemoryCache _cache;
    private readonly ISystemConfigProvider _config;

    public EnrollmentForecastService(AppDbContext context, IGpaCalculationService gpaCalculationService, IMemoryCache cache, ISystemConfigProvider? config = null)
    {
        _context = context;
        _gpaCalculationService = gpaCalculationService;
        _cache = cache;
        _config = config ?? new NullSystemConfigProvider();
    }

    public async Task<CourseDemandForecastDto?> GetCourseDemandForecastAsync(int courseId)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course == null)
            return null;

        var forecastContext = await GetOrBuildForecastContextAsync();
        return BuildCourseDemandForecast(forecastContext, course);
    }

    public void InvalidateForecastContext()
    {
        _cache.Remove(ForecastContextCacheKey);
    }

    public async Task<DepartmentForecastSummaryDto?> GetDepartmentSummaryAsync(int departmentId)
    {
        var department = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == departmentId);

        if (department == null)
            return null;

        var forecastContext = await GetOrBuildForecastContextAsync();

        var relevantCourseIds = new HashSet<int>(
            forecastContext.Courses.Values
                .Where(course => course.DepartmentId == departmentId)
                .Where(course =>
                    forecastContext.ActiveCurriculumCourseIds.Contains(course.Id)
                    || forecastContext.ActiveTermSectionCourseIds.Contains(course.Id)
                    || forecastContext.PredictedDemandCourseIds.Contains(course.Id))
                .Select(course => course.Id));

        var courseSummaries = relevantCourseIds
            .Select(courseId => BuildCourseDemandForecast(forecastContext, forecastContext.Courses[courseId]))
            .OrderBy(summary => summary.CourseCode)
            .ToList();

        var currentSectionCount = forecastContext.ActiveTerm != null
            ? forecastContext.AllSections
                .Count(s => relevantCourseIds.Contains(s.CourseId)
                    && s.TermId == forecastContext.ActiveTerm.Id
                    && s.IsActive)
            : 0;

        var totalRecommended = courseSummaries.Sum(s => s.RecommendedSectionCount);

        return new DepartmentForecastSummaryDto
        {
            DepartmentId = department.Id,
            DepartmentName = department.Name,
            TotalCoursesAnalyzed = courseSummaries.Count,
            TotalPredictedDemand = courseSummaries.Sum(summary => summary.TotalPredictedDemand),
            TotalRecommendedSections = totalRecommended,
            CourseSummaries = courseSummaries,
            Notes = BuildDepartmentNotes(forecastContext, courseSummaries),
            CapacitySummary = new DepartmentCapacitySummaryDto
            {
                TotalCurrentSections = currentSectionCount,
                TotalRecommendedSections = totalRecommended,
                SectionDelta = totalRecommended - currentSectionCount,
                CoursesNeedingMoreSections = courseSummaries
                    .Count(s => s.RecommendedSectionCount > (
                        forecastContext.ActiveTerm != null
                            ? forecastContext.AllSections.Count(sec =>
                                sec.CourseId == s.CourseId
                                && sec.TermId == forecastContext.ActiveTerm.Id
                                && sec.IsActive)
                            : 0)),
                CoursesWithExcessCapacity = courseSummaries
                    .Count(s => s.SectionDistribution.UtilizationRate < 0.50m && s.SectionDistribution.UtilizationRate > 0)
            }
        };
    }

    public async Task<int> CalibrateForTermAsync(int termId, CancellationToken ct = default)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");

        if (term.Status < TermStatus.AddDrop)
            throw new InvalidOperationException("Kalibrasyon için dönem en az ek/bırak aşamasını geçmiş olmalı.");

        // Already calibrated?
        var existingCount = await _context.ForecastCalibrations.CountAsync(fc => fc.TermId == termId, ct);
        if (existingCount > 0)
            return 0;

        var context = await GetOrBuildForecastContextAsync();

        // Actual enrollments for this term
        var actualEnrollments = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(r => r.CourseSection).ThenInclude(s => s.Course)
            .Where(r => r.CourseSection.TermId == termId
                && (r.Status == RegistrationStatus.Active || r.Status == RegistrationStatus.Completed))
            .GroupBy(r => new { r.CourseSection.CourseId, DeptId = r.CourseSection.Course.DepartmentId })
            .Select(g => new
            {
                CourseId = g.Key.CourseId,
                DepartmentId = g.Key.DeptId ?? 0,
                ActualCount = g.Select(r => r.StudentId).Distinct().Count()
            })
            .ToListAsync(ct);

        var actualSections = await _context.CourseSections
            .AsNoTracking()
            .Where(s => s.TermId == termId && s.IsActive)
            .GroupBy(s => s.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count, ct);

        var calibrations = new List<ForecastCalibration>();

        foreach (var actual in actualEnrollments)
        {
            if (!context.Courses.TryGetValue(actual.CourseId, out var course))
                continue;

            var forecast = BuildCourseDemandForecast(context, course);
            var absError = Math.Abs(forecast.TotalPredictedDemand - actual.ActualCount);
            var pctError = actual.ActualCount > 0
                ? (decimal)absError / actual.ActualCount * 100
                : 0;

            var ruleBasedEligible = forecast.PredictedEligibleCount + forecast.PredictedRetakeCount + forecast.PredictedOverloadOrUpperClassCount;
            var showRate = ruleBasedEligible > 0
                ? Math.Min(1.0m, (decimal)actual.ActualCount / ruleBasedEligible)
                : 0.90m;

            calibrations.Add(new ForecastCalibration
            {
                CourseId = actual.CourseId,
                TermId = termId,
                DepartmentId = actual.DepartmentId,
                PredictedDemand = forecast.TotalPredictedDemand,
                ActualEnrollment = actual.ActualCount,
                PredictedSections = forecast.RecommendedSectionCount,
                ActualSections = actualSections.GetValueOrDefault(actual.CourseId),
                AbsoluteError = absError,
                PercentageError = pctError,
                RuleBasedWeight = 0.55m,
                HistoricalWeight = 0.45m,
                EligibleStudentCount = ruleBasedEligible,
                ShowRate = showRate,
                CalibratedAt = DateTime.UtcNow,
            });
        }

        _context.ForecastCalibrations.AddRange(calibrations);
        await _context.SaveChangesAsync(ct);
        return calibrations.Count;
    }

    // ═══════════════════════════════════════════════════════════════
    //  FORECAST CONTEXT BUILDER
    // ═══════════════════════════════════════════════════════════════

    private async Task<ForecastContextData> GetOrBuildForecastContextAsync()
    {
        if (_cache.TryGetValue(ForecastContextCacheKey, out ForecastContextData? cached) && cached != null)
            return cached;

        var data = await BuildForecastContextCoreAsync();
        _cache.Set(ForecastContextCacheKey, data, CacheDuration);
        return data;
    }

    private async Task<ForecastContextData> BuildForecastContextCoreAsync()
    {
        var activeTerm = await _context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(term => term.IsActive);

        var allTerms = await _context.AcademicTerms
            .AsNoTracking()
            .OrderBy(t => t.Year).ThenBy(t => t.Semester)
            .ToListAsync();

        var activeStudents = await _context.Students
            .AsNoTracking()
            .Include(student => student.StudentPrograms)
                .ThenInclude(program => program.Program)
                    .ThenInclude(program => program.Department)
            .Where(student => student.Status == StudentStatus.Active && !student.HasFinancialBlock)
            .ToListAsync();

        var activeMainPrograms = activeStudents
            .Select(student => new
            {
                Student = student,
                Program = student.StudentPrograms
                    .FirstOrDefault(program => program.Type == StudentProgramType.Main && program.IsActive)
            })
            .Where(item => item.Program != null)
            .ToDictionary(item => item.Student.Id, item => item.Program!);

        var activeStudentIds = activeMainPrograms.Keys.ToList();
        var activeCurriculumIds = activeMainPrograms.Values
            .Select(program => program.CurriculumId)
            .Distinct()
            .ToList();

        var courses = await _context.Courses
            .AsNoTracking()
            .ToDictionaryAsync(course => course.Id);

        var curriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(curriculumCourse => activeCurriculumIds.Contains(curriculumCourse.CurriculumId))
            .ToListAsync();

        var curriculumCoursesByCurriculumId = curriculumCourses
            .GroupBy(curriculumCourse => curriculumCourse.CurriculumId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(curriculumCourse => curriculumCourse.SuggestedSemester)
                    .ThenBy(curriculumCourse => courses.TryGetValue(curriculumCourse.CourseId, out var course)
                        ? course.Code
                        : curriculumCourse.CourseId.ToString())
                    .ToList());

        var prerequisites = await _context.CoursePrerequisites
            .AsNoTracking()
            .ToListAsync();

        var prerequisitesByCourseId = prerequisites
            .GroupBy(prerequisite => prerequisite.CourseId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(prerequisite => prerequisite.RequiredCourseId)
                    .ToHashSet());

        var equivalencyPairs = await _context.CourseEquivalencies
            .AsNoTracking()
            .ToListAsync();

        var equivalentCourseIdsByCourseId = BuildEquivalentCourseMap(equivalencyPairs);

        var allSections = await _context.CourseSections
            .AsNoTracking()
            .Include(section => section.Course)
            .Include(section => section.Classroom)
            .ToListAsync();

        var registrations = activeStudentIds.Count == 0
            ? new List<CourseRegistration>()
            : await _context.CourseRegistrations
                .AsNoTracking()
                .Include(registration => registration.CourseSection)
                    .ThenInclude(section => section.Course)
                .Where(registration => activeStudentIds.Contains(registration.StudentId))
                .ToListAsync();

        var registrationsByStudentId = registrations
            .GroupBy(registration => registration.StudentId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var activeTermRegistrationsByStudentId = activeTerm == null
            ? new Dictionary<int, List<CourseRegistration>>()
            : registrations
                .Where(registration =>
                    registration.CourseSection.TermId == activeTerm.Id
                    && RegistrationRulePolicy.IsDemandStatus(registration.Status))
                .GroupBy(registration => registration.StudentId)
                .ToDictionary(group => group.Key, group => group.ToList());

        // Historical enrollment counts per course per term (from ALL registrations, not just active students)
        var historicalEnrollments = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(r => r.CourseSection)
            .Where(r => r.Status == RegistrationStatus.Active || r.Status == RegistrationStatus.Completed)
            .GroupBy(r => new { r.CourseSection.CourseId, r.CourseSection.TermId })
            .Select(g => new HistoricalEnrollmentRecord
            {
                CourseId = g.Key.CourseId,
                TermId = g.Key.TermId,
                EnrollmentCount = g.Select(r => r.StudentId).Distinct().Count()
            })
            .ToListAsync();

        var historicalByCourseThenTerm = historicalEnrollments
            .GroupBy(h => h.CourseId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(h => h.TermId, h => h.EnrollmentCount));

        var sectionCountByCourseTerm = allSections
            .Where(s => s.IsActive)
            .GroupBy(s => new { s.CourseId, s.TermId })
            .ToDictionary(g => (g.Key.CourseId, g.Key.TermId), g => g.Count());

        var termsById = allTerms.ToDictionary(t => t.Id);

        var approvalFeedbackByCourse = new Dictionary<int, List<ApprovalFeedbackRecord>>();

        // Show rate from calibration data
        var calibrations = await _context.ForecastCalibrations
            .AsNoTracking()
            .Where(fc => fc.ActualEnrollment > 0 && fc.EligibleStudentCount > 0)
            .ToListAsync();

        var showRateByCourse = calibrations
            .GroupBy(fc => fc.CourseId)
            .ToDictionary(
                g => g.Key,
                g => Math.Min(1.0m, g.Average(fc => fc.ShowRate)));

        // Adaptive weights from calibration data
        var adaptiveWeights = new Dictionary<int, (decimal RuleWeight, decimal HistoricalWeight)>();
        foreach (var group in calibrations.GroupBy(fc => fc.CourseId))
        {
            var records = group.OrderByDescending(fc => fc.TermId).Take(4).ToList();
            if (records.Count < 2) continue;
            var avgPctError = records.Average(r => r.PercentageError);
            var ruleWeight = avgPctError switch
            {
                > 30m => 0.35m,
                > 20m => 0.45m,
                > 10m => 0.55m,
                _ => 0.65m
            };
            adaptiveWeights[group.Key] = (ruleWeight, 1.0m - ruleWeight);
        }

        // Cohort growth by department
        var cohortData = await _context.StudentPrograms
            .AsNoTracking()
            .Where(sp => sp.Type == StudentProgramType.Main && sp.IsActive)
            .Select(sp => new { DeptId = sp.Program.DepartmentId, Year = sp.EnrollmentDate.Year })
            .ToListAsync();

        var cohortGrowthByDept = new Dictionary<int, decimal>();
        foreach (var deptGroup in cohortData.GroupBy(c => c.DeptId))
        {
            var yearCounts = deptGroup.GroupBy(c => c.Year)
                .OrderBy(g => g.Key)
                .Select(g => g.Count())
                .ToList();
            if (yearCounts.Count >= 2)
            {
                var lastYear = yearCounts[^1];
                var prevAvg = yearCounts.Take(yearCounts.Count - 1).Average();
                cohortGrowthByDept[deptGroup.Key] = prevAvg > 0 ? (decimal)lastYear / (decimal)prevAvg : 1.0m;
            }
        }

        // Classroom capacities by faculty
        List<(int FacultyId, int Capacity)> classroomCapList;
        if (activeTerm != null)
        {
            var rawClassrooms = await _context.ClassroomTermAssignments
                .AsNoTracking()
                .Where(cta => cta.TermId == activeTerm.Id)
                .Include(cta => cta.Classroom)
                .Where(cta => cta.Classroom != null)
                .Select(cta => new { cta.FacultyId, cta.Classroom!.Capacity })
                .ToListAsync();
            classroomCapList = rawClassrooms.Select(x => (x.FacultyId, x.Capacity)).ToList();
        }
        else
        {
            classroomCapList = new List<(int, int)>();
        }

        var classroomCapsByFaculty = classroomCapList
            .GroupBy(c => c.FacultyId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Capacity).OrderByDescending(c => c).ToList());

        // Department-Faculty map
        var departmentFacultyMap = await _context.Departments
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.FacultyId);

        var cgpaCache = new Dictionary<int, decimal>();
        var studentSnapshots = new List<StudentForecastSnapshot>();

        foreach (var student in activeStudents)
        {
            if (!activeMainPrograms.TryGetValue(student.Id, out var activeProgram))
                continue;

            var studentRegistrations = registrationsByStudentId.GetValueOrDefault(student.Id, new List<CourseRegistration>());
            var activeTermRegistrations = activeTermRegistrationsByStudentId.GetValueOrDefault(student.Id, new List<CourseRegistration>());

            var snapshot = await BuildStudentSnapshotAsync(
                student,
                activeProgram,
                studentRegistrations,
                activeTermRegistrations,
                courses,
                curriculumCoursesByCurriculumId,
                prerequisitesByCourseId,
                equivalentCourseIdsByCourseId,
                cgpaCache);

            studentSnapshots.Add(snapshot);
        }

        return new ForecastContextData
        {
            ActiveTerm = activeTerm,
            AllTerms = allTerms,
            TermsById = termsById,
            Courses = courses,
            AllSections = allSections,
            StudentSnapshots = studentSnapshots,
            ActiveCurriculumCourseIds = curriculumCourses.Select(curriculumCourse => curriculumCourse.CourseId).ToHashSet(),
            ActiveTermSectionCourseIds = activeTerm == null
                ? new HashSet<int>()
                : allSections
                    .Where(section => section.TermId == activeTerm.Id && section.IsActive)
                    .Select(section => section.CourseId)
                    .ToHashSet(),
            HistoricalEnrollmentsByCourse = historicalByCourseThenTerm,
            SectionCountByCourseTerm = sectionCountByCourseTerm,
            GlobalNotes = BuildGlobalNotes(activeTerm, activeStudents.Count, activeMainPrograms.Count),
            ApprovalFeedbackByCourse = approvalFeedbackByCourse,
            ShowRateByCourse = showRateByCourse,
            CohortGrowthByDepartment = cohortGrowthByDept,
            ClassroomCapacitiesByFaculty = classroomCapsByFaculty,
            DepartmentFacultyMap = departmentFacultyMap,
            AdaptiveWeightsByCourse = adaptiveWeights
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  STUDENT SNAPSHOT BUILDER (enhanced with grade-level breakdown)
    // ═══════════════════════════════════════════════════════════════

    private async Task<StudentForecastSnapshot> BuildStudentSnapshotAsync(
        Student student,
        StudentProgram activeProgram,
        List<CourseRegistration> registrations,
        List<CourseRegistration> activeTermRegistrations,
        IReadOnlyDictionary<int, Course> courses,
        IReadOnlyDictionary<int, List<CurriculumCourse>> curriculumCoursesByCurriculumId,
        IReadOnlyDictionary<int, HashSet<int>> prerequisitesByCourseId,
        IReadOnlyDictionary<int, HashSet<int>> equivalentCourseIdsByCourseId,
        IDictionary<int, decimal> cgpaCache)
    {
        var cgpa = await GetEffectiveCgpaAsync(student, cgpaCache);
        var ectsLimitBelow200 = await _config.GetIntAsync("Registration.EctsLimit.CgpaBelow200", 30);
        var ectsLimit200To249 = await _config.GetIntAsync("Registration.EctsLimit.Cgpa200To249", 42);
        var ectsLimitAbove250 = await _config.GetIntAsync("Registration.EctsLimit.CgpaAbove250", 48);
        var ectsLimit = RegistrationRulePolicy.GetEctsLimit(cgpa, ectsLimitBelow200, ectsLimit200To249, ectsLimitAbove250);
        var completedSemesters = Math.Max(0, student.CompletedSemesters);
        var currentSemester = completedSemesters + 1;

        var passedCourseIds = registrations
            .Where(registration => RegistrationRulePolicy.IsPassedGrade(registration.Grade))
            .Select(registration => registration.CourseSection.CourseId)
            .ToHashSet();

        // Detailed failure classification: last grade per failed course
        var failedCoursesWithGrade = registrations
            .Where(r => RegistrationRulePolicy.IsFailedGrade(r.Grade))
            .GroupBy(r => r.CourseSection.CourseId)
            .Where(g => !passedCourseIds.Contains(g.Key))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.CreatedAt).First().Grade);

        var failedCourseIds = failedCoursesWithGrade.Keys.ToHashSet();

        // Split by grade type
        var retakeFFIds = failedCoursesWithGrade
            .Where(kv => kv.Value == GradeCode.FF)
            .Select(kv => kv.Key).ToHashSet();
        var retakeNAIds = failedCoursesWithGrade
            .Where(kv => kv.Value == GradeCode.NA)
            .Select(kv => kv.Key).ToHashSet();
        var retakeUIds = failedCoursesWithGrade
            .Where(kv => kv.Value == GradeCode.U)
            .Select(kv => kv.Key).ToHashSet();

        var currentTermSelectedCourseIds = activeTermRegistrations
            .Select(registration => registration.CourseSection.CourseId)
            .ToHashSet();

        var regularCourseIds = new HashSet<int>();
        var behindScheduleCourseIds = new HashSet<int>();
        var upperCourseIds = new HashSet<int>();
        var ectsLimitedCourseIds = new HashSet<int>();

        // Preserve already-selected demand first
        if (currentTermSelectedCourseIds.Count > 0)
        {
            foreach (var selectedCourseId in currentTermSelectedCourseIds)
            {
                if (failedCourseIds.Contains(selectedCourseId))
                    continue;

                var sugSem = GetSuggestedSemester(selectedCourseId, activeProgram.CurriculumId, curriculumCoursesByCurriculumId);
                if (sugSem.HasValue && sugSem.Value > currentSemester)
                    upperCourseIds.Add(selectedCourseId);
                else if (sugSem.HasValue && sugSem.Value < currentSemester)
                    behindScheduleCourseIds.Add(selectedCourseId);
                else
                    regularCourseIds.Add(selectedCourseId);
            }
        }

        var reservedEcts = SumCourseEcts(currentTermSelectedCourseIds, courses);
        reservedEcts += SumCourseEcts(failedCourseIds.Except(currentTermSelectedCourseIds), courses);
        var remainingEcts = Math.Max(0, ectsLimit - reservedEcts);

        var courseCandidates = new List<CourseCandidate>();

        var retentionThreshold = await _config.GetDecimalAsync("GPA.RetentionThreshold", 1.70m);

        if (curriculumCoursesByCurriculumId.TryGetValue(activeProgram.CurriculumId, out var curriculumCourses))
        {
            foreach (var curriculumCourse in curriculumCourses)
            {
                if (curriculumCourse.SuggestedSemester <= 0)
                    continue;

                if (!courses.TryGetValue(curriculumCourse.CourseId, out var course))
                    continue;

                if (currentTermSelectedCourseIds.Contains(course.Id) || failedCourseIds.Contains(course.Id))
                    continue;

                if (passedCourseIds.Contains(course.Id))
                    continue;

                if (!ArePrerequisitesMet(course.Id, passedCourseIds, prerequisitesByCourseId, equivalentCourseIdsByCourseId))
                    continue;

                if (RegistrationRulePolicy.HasLowCgpaRestriction(cgpa, completedSemesters, retentionThreshold)
                    && !RegistrationRulePolicy.IsAllowedUnderLowCgpaRestriction(
                        course,
                        curriculumCourse.SuggestedSemester,
                        completedSemesters,
                        activeProgram.Program?.DepartmentId,
                        false,
                        false))
                {
                    continue;
                }

                var candidate = new CourseCandidate(
                    course.Id,
                    course.Ects,
                    curriculumCourse.SuggestedSemester,
                    course.Code,
                    curriculumCourse.SuggestedSemester > currentSemester
                        ? CourseCandidateBucket.UpperCourse
                        : curriculumCourse.SuggestedSemester < currentSemester
                            ? CourseCandidateBucket.BehindSchedule
                            : CourseCandidateBucket.Regular,
                    CalculateCandidatePriority(curriculumCourse.SuggestedSemester, currentSemester, cgpa));

                courseCandidates.Add(candidate);
            }
        }

        SelectCandidatesWithinEctsLimit(
            courseCandidates,
            regularCourseIds,
            behindScheduleCourseIds,
            upperCourseIds,
            ref remainingEcts);

        // Track candidates that were eligible but couldn't fit
        foreach (var candidate in courseCandidates)
        {
            if (!regularCourseIds.Contains(candidate.CourseId)
                && !behindScheduleCourseIds.Contains(candidate.CourseId)
                && !upperCourseIds.Contains(candidate.CourseId)
                && !failedCourseIds.Contains(candidate.CourseId))
            {
                ectsLimitedCourseIds.Add(candidate.CourseId);
            }
        }

        // Clean up overlaps
        regularCourseIds.ExceptWith(failedCourseIds);
        behindScheduleCourseIds.ExceptWith(failedCourseIds);
        behindScheduleCourseIds.ExceptWith(regularCourseIds);
        upperCourseIds.ExceptWith(failedCourseIds);
        upperCourseIds.ExceptWith(regularCourseIds);
        upperCourseIds.ExceptWith(behindScheduleCourseIds);

        return new StudentForecastSnapshot
        {
            StudentId = student.Id,
            DepartmentId = activeProgram.Program?.DepartmentId ?? 0,
            DepartmentName = activeProgram.Program?.Department?.Name ?? string.Empty,
            ProgramName = activeProgram.Program?.Name ?? string.Empty,
            RetakeCourseIds = failedCourseIds,
            RetakeFFCourseIds = retakeFFIds,
            RetakeNACourseIds = retakeNAIds,
            RetakeUCourseIds = retakeUIds,
            RegularCourseIds = regularCourseIds,
            BehindScheduleCourseIds = behindScheduleCourseIds,
            UpperCourseIds = upperCourseIds,
            EctsLimitedCourseIds = ectsLimitedCourseIds
        };
    }

    private static int? GetSuggestedSemester(
        int courseId,
        int curriculumId,
        IReadOnlyDictionary<int, List<CurriculumCourse>> curriculumCoursesByCurriculumId)
    {
        if (!curriculumCoursesByCurriculumId.TryGetValue(curriculumId, out var courses))
            return null;
        return courses.FirstOrDefault(cc => cc.CourseId == courseId)?.SuggestedSemester;
    }

    // ═══════════════════════════════════════════════════════════════
    //  DEMAND FORECAST BUILDER (enhanced)
    // ═══════════════════════════════════════════════════════════════

    private CourseDemandForecastDto BuildCourseDemandForecast(ForecastContextData context, Course course)
    {
        // --- Detailed breakdown ---
        var retakeFFStudents = context.StudentSnapshots
            .Where(s => s.RetakeFFCourseIds.Contains(course.Id))
            .Select(s => s.StudentId).ToHashSet();

        var retakeNAStudents = context.StudentSnapshots
            .Where(s => s.RetakeNACourseIds.Contains(course.Id))
            .Select(s => s.StudentId).ToHashSet();

        var retakeUStudents = context.StudentSnapshots
            .Where(s => s.RetakeUCourseIds.Contains(course.Id))
            .Select(s => s.StudentId).ToHashSet();

        var retakeStudents = context.StudentSnapshots
            .Where(s => s.RetakeCourseIds.Contains(course.Id))
            .Select(s => s.StudentId).ToHashSet();

        var regularStudents = context.StudentSnapshots
            .Where(s => s.RegularCourseIds.Contains(course.Id))
            .Select(s => s.StudentId).ToHashSet();
        regularStudents.ExceptWith(retakeStudents);

        var behindScheduleStudents = context.StudentSnapshots
            .Where(s => s.BehindScheduleCourseIds.Contains(course.Id))
            .Select(s => s.StudentId).ToHashSet();
        behindScheduleStudents.ExceptWith(retakeStudents);
        behindScheduleStudents.ExceptWith(regularStudents);

        var upperStudents = context.StudentSnapshots
            .Where(s => s.UpperCourseIds.Contains(course.Id))
            .Select(s => s.StudentId).ToHashSet();
        upperStudents.ExceptWith(retakeStudents);
        upperStudents.ExceptWith(regularStudents);
        upperStudents.ExceptWith(behindScheduleStudents);

        var ectsLimitedStudents = context.StudentSnapshots
            .Where(s => s.EctsLimitedCourseIds.Contains(course.Id))
            .Select(s => s.StudentId).ToHashSet();

        var ruleBasedTotal = retakeStudents.Count + regularStudents.Count
            + behindScheduleStudents.Count + upperStudents.Count;
        var predictedStudentIds = retakeStudents
            .Concat(regularStudents)
            .Concat(behindScheduleStudents)
            .Concat(upperStudents)
            .ToHashSet();

        // --- Show rate adjustment ---
        var showRate = context.ShowRateByCourse.GetValueOrDefault(course.Id, 0.90m);
        var adjustedRuleBasedTotal = (int)Math.Round(ruleBasedTotal * showRate);

        // --- Cohort growth for courses ---
        if (course.DepartmentId.HasValue)
        {
            var cohortGrowth = context.CohortGrowthByDepartment.GetValueOrDefault(course.DepartmentId.Value, 1.0m);
            if (cohortGrowth != 1.0m)
            {
                var cohortAdj = 1.0m + (cohortGrowth - 1.0m) * 0.5m; // 50% dampening
                adjustedRuleBasedTotal = (int)Math.Round(adjustedRuleBasedTotal * cohortAdj);
            }
        }

        // --- Historical analysis ---
        var historicalData = BuildHistoricalEnrollments(context, course.Id);
        var historicalCounts = historicalData.Select(h => h.ActualEnrollment).ToList();
        var comparableHistoricalData = BuildComparableHistoricalEnrollments(context, historicalData);
        var comparableHistoricalCounts = comparableHistoricalData.Select(h => h.ActualEnrollment).ToList();

        // --- Hybrid blending ---
        var adaptiveRuleWeight = context.AdaptiveWeightsByCourse.TryGetValue(course.Id, out var aw) ? (decimal?)aw.RuleWeight : null;
        var blendedPrediction = BlendPrediction(adjustedRuleBasedTotal, historicalCounts, comparableHistoricalCounts, adaptiveRuleWeight);

        // --- Trend adjustment ---
        var finalPrediction = ApplyTrendAdjustment(blendedPrediction, historicalCounts, comparableHistoricalCounts);

        // --- Confidence interval ---
        var confidence = CalculateConfidence(finalPrediction, comparableHistoricalCounts);

        // --- Section distribution ---
        var capacityRecommendation = BuildCapacityRecommendation(context, course);
        var recommendedSections = CalculateRecommendedSectionCount(finalPrediction, capacityRecommendation.EffectiveCapacity);

        // Classroom constraint
        var classroomConstraintNotes = new List<string>();
        if (course.DepartmentId.HasValue)
        {
            var facultyId = context.DepartmentFacultyMap.GetValueOrDefault(course.DepartmentId.Value);
            if (facultyId > 0 && context.ClassroomCapacitiesByFaculty.TryGetValue(facultyId, out var roomCaps) && roomCaps.Count > 0)
            {
                var maxRoom = roomCaps[0];
                if (capacityRecommendation.EffectiveCapacity > maxRoom && maxRoom > 0)
                {
                    classroomConstraintNotes.Add($"Şube kapasitesi ({capacityRecommendation.EffectiveCapacity}) fakültedeki en büyük derslikten ({maxRoom}) büyük. Derslik boyutuna indirildi.");
                    capacityRecommendation.EffectiveCapacity = maxRoom;
                    // Recalculate sections with new capacity
                    recommendedSections = CalculateRecommendedSectionCount(finalPrediction, capacityRecommendation.EffectiveCapacity);
                }
            }
        }

        var approvalFeedback = ApplyApprovalFeedback(context, course.Id, recommendedSections, finalPrediction);
        recommendedSections = approvalFeedback.AdjustedSectionCount;
        var sectionDistribution = BuildSectionDistribution(
            finalPrediction, recommendedSections, capacityRecommendation.EffectiveCapacity);
        var crossDepartmentDemand = BuildCrossDepartmentDemand(context, course, predictedStudentIds);
        var comparableTermEnrollment = comparableHistoricalData.LastOrDefault()?.ActualEnrollment;

        // --- Notes ---
        var notes = new List<string>(context.GlobalNotes);

        if (!context.ActiveCurriculumCourseIds.Contains(course.Id))
            notes.Add("Bu ders aktif mufredatlarda yer almiyor; tahmin agirlikla tekrarlar ve mevcut aktif kayitlar uzerinden uretilir.");

        if (!context.ActiveTermSectionCourseIds.Contains(course.Id))
            notes.Add("Bu ders icin aktif donemde section bulunmuyor; talep latent ihtiyac olarak hesaplandi.");

        if (comparableHistoricalCounts.Count >= 2)
            notes.Add($"Tahmin, guz/bahar mevsimselligi korunarak {comparableHistoricalCounts.Count} benzer donem verisiyle agirliklandirildi.");
        else if (historicalCounts.Count >= 3)
            notes.Add($"Tahmin, {historicalCounts.Count} donemlik gecmis veri ile harmanlanarak uretildi (agirlik: %55 kural, %45 tarihsel).");
        else if (historicalCounts.Count >= 1)
            notes.Add($"Tahmin, {historicalCounts.Count} donemlik gecmis veri ile harmanlanarak uretildi (agirlik: %75 kural, %25 tarihsel).");
        else
            notes.Add("Gecmis donem verisi bulunamadigi icin tahmin tamamen kural tabanli uretildi.");

        if (crossDepartmentDemand.Count > 0)
            notes.Add($"Diger bolumlerden {crossDepartmentDemand.Sum(item => item.EstimatedStudentCount)} ogrencilik gercek talep tespit edildi.");

        notes.AddRange(capacityRecommendation.Notes);
        notes.AddRange(approvalFeedback.Notes);
        notes.AddRange(classroomConstraintNotes);

        return new CourseDemandForecastDto
        {
            CourseId = course.Id,
            CourseCode = course.Code,
            CourseName = course.Name,
            PredictedEligibleCount = regularStudents.Count + behindScheduleStudents.Count,
            PredictedRetakeCount = retakeStudents.Count,
            PredictedOverloadOrUpperClassCount = upperStudents.Count,
            TotalPredictedDemand = finalPrediction,
            RecommendedSectionCount = recommendedSections,
            CapacityBasis = capacityRecommendation.CapacityBasis,
            ComparableTermEnrollment = comparableTermEnrollment,
            Notes = notes.Distinct().ToList(),

            // Enhanced fields
            DemandBreakdown = new DemandBreakdownDto
            {
                RetakeFF = retakeFFStudents.Count,
                RetakeNA = retakeNAStudents.Count,
                RetakeU = retakeUStudents.Count,
                RegularOnSchedule = regularStudents.Count,
                BehindSchedule = behindScheduleStudents.Count,
                UpperCourse = upperStudents.Count,
                EctsLimited = ectsLimitedStudents.Count
            },
            HistoricalEnrollments = historicalData,
            Confidence = confidence,
            SectionDistribution = sectionDistribution,
            CrossDepartmentDemand = crossDepartmentDemand,
            ShowRate = showRate
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  HISTORICAL ANALYSIS
    // ═══════════════════════════════════════════════════════════════

    private List<HistoricalEnrollmentDto> BuildHistoricalEnrollments(ForecastContextData context, int courseId)
    {
        if (!context.HistoricalEnrollmentsByCourse.TryGetValue(courseId, out var termEnrollments))
            return new List<HistoricalEnrollmentDto>();

        var results = new List<HistoricalEnrollmentDto>();

        foreach (var (termId, count) in termEnrollments.OrderBy(kv =>
            context.TermsById.TryGetValue(kv.Key, out var t) ? t.Year * 10 + (int)t.Semester : kv.Key))
        {
            var term = context.TermsById.GetValueOrDefault(termId);
            var sectionCount = context.SectionCountByCourseTerm.GetValueOrDefault((courseId, termId));

            results.Add(new HistoricalEnrollmentDto
            {
                TermId = termId,
                TermName = term?.Name ?? $"Term {termId}",
                Year = term?.Year ?? 0,
                ActualEnrollment = count,
                SectionCount = sectionCount
            });
        }

        return results;
    }

    private static int BlendPrediction(int ruleBasedTotal, List<int> historicalCounts, List<int> comparableHistoricalCounts, decimal? adaptiveRuleWeight = null)
    {
        if (historicalCounts.Count == 0)
            return ruleBasedTotal;

        var historicalAvg = historicalCounts.Average();
        var seasonalAvg = comparableHistoricalCounts.Count > 0
            ? comparableHistoricalCounts.Average()
            : historicalAvg;
        var historicalSignal = comparableHistoricalCounts.Count switch
        {
            >= 2 => (seasonalAvg * 0.70) + (historicalAvg * 0.30),
            1 => (seasonalAvg * 0.55) + (historicalAvg * 0.45),
            _ => historicalAvg
        };

        double ruleWeight;
        if (adaptiveRuleWeight.HasValue)
        {
            ruleWeight = (double)adaptiveRuleWeight.Value;
        }
        else
        {
            ruleWeight = historicalCounts.Count >= 3 ? 0.55 : 0.75;
        }

        var blended = ruleWeight * ruleBasedTotal + (1.0 - ruleWeight) * historicalSignal;

        return (int)Math.Round(blended);
    }

    private static int ApplyTrendAdjustment(int prediction, List<int> historicalCounts, List<int> comparableHistoricalCounts)
    {
        var trendSource = comparableHistoricalCounts.Count >= 2
            ? comparableHistoricalCounts
            : historicalCounts;

        if (trendSource.Count < 2)
            return prediction;

        var weights = new[] { 0.15, 0.20, 0.25, 0.40 };
        var recent = trendSource.TakeLast(4).ToList();
        var applicableWeights = weights.TakeLast(recent.Count).ToArray();
        var totalWeight = applicableWeights.Sum();

        var weightedAvg = 0.0;
        for (var i = 0; i < recent.Count; i++)
            weightedAvg += recent[i] * (applicableWeights[i] / totalWeight);

        var simpleAvg = trendSource.Average();
        if (simpleAvg <= 0)
            return prediction;

        var trendFactor = Math.Clamp(weightedAvg / simpleAvg, 0.85, 1.20);
        return (int)Math.Round(prediction * trendFactor);
    }

    private static ConfidenceRangeDto CalculateConfidence(int prediction, List<int> comparableHistoricalCounts)
    {
        var recent = comparableHistoricalCounts.TakeLast(4).ToList();

        if (recent.Count >= 2)
        {
            var mean = recent.Average();
            var variance = recent.Sum(x => Math.Pow(x - mean, 2)) / recent.Count;
            var stdDev = Math.Sqrt(variance);
            var margin = (int)Math.Ceiling(stdDev * 1.96);
            margin = Math.Max(margin, (int)Math.Ceiling(prediction * 0.10));

            return new ConfidenceRangeDto
            {
                LowerBound = Math.Max(0, prediction - margin),
                UpperBound = prediction + margin,
                Margin = margin,
                Method = $"Son {recent.Count} karşılaştırılabilir dönem"
            };
        }

        var ruleOfThumbMargin = Math.Max(5, (int)Math.Ceiling(prediction * 0.15));
        return new ConfidenceRangeDto
        {
            LowerBound = Math.Max(0, prediction - ruleOfThumbMargin),
            UpperBound = prediction + ruleOfThumbMargin,
            Margin = ruleOfThumbMargin,
            Method = "Tahmini (±%15)"
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  SECTION DISTRIBUTION
    // ═══════════════════════════════════════════════════════════════

    private static SectionDistributionDto BuildSectionDistribution(
        int totalDemand, int recommendedSections, int effectiveCapacity)
    {
        var distribution = new SectionDistributionDto
        {
            RecommendedSections = recommendedSections,
            EffectiveCapacityPerSection = effectiveCapacity
        };

        if (recommendedSections <= 0 || effectiveCapacity <= 0)
        {
            distribution.Warnings.Add("Tahmin veya kapasite verisi yetersiz; dagitim hesaplanamadi.");
            return distribution;
        }

        distribution.TotalCapacity = recommendedSections * effectiveCapacity;
        distribution.UtilizationRate = distribution.TotalCapacity > 0
            ? Math.Round((decimal)totalDemand / distribution.TotalCapacity, 3)
            : 0;
        distribution.SurplusCapacity = distribution.TotalCapacity - totalDemand;

        // Even distribution
        var basePerSection = totalDemand / recommendedSections;
        var remainder = totalDemand % recommendedSections;

        for (var i = 0; i < recommendedSections; i++)
        {
            distribution.StudentsPerSection.Add(basePerSection + (i < remainder ? 1 : 0));
        }

        // Warnings
        if (distribution.UtilizationRate > 0.95m)
            distribution.Warnings.Add("Kapasite kritik seviyede (doluluk > %95). Ek sube veya kapasite artisi degerlendirilmeli.");
        else if (distribution.UtilizationRate > 0.85m)
            distribution.Warnings.Add("Yuksek doluluk orani (> %85).");

        if (distribution.SurplusCapacity < 0)
            distribution.Warnings.Add($"Kapasite yetersizligi: {Math.Abs(distribution.SurplusCapacity)} ogrencilik acik.");

        if (distribution.UtilizationRate > 0 && distribution.UtilizationRate < 0.50m)
            distribution.Warnings.Add("Dusuk doluluk orani (< %50). Sube birlestirme degerlendirilebilir.");

        return distribution;
    }

    // ═══════════════════════════════════════════════════════════════
    //  CAPACITY RECOMMENDATION (existing logic preserved)
    // ═══════════════════════════════════════════════════════════════

    private CapacityRecommendation BuildCapacityRecommendation(ForecastContextData context, Course course)
    {
        CapacityRecommendation recommendation;

        if (context.ActiveTerm != null)
        {
            var activeSections = context.AllSections
                .Where(section => section.CourseId == course.Id
                    && section.TermId == context.ActiveTerm.Id
                    && section.IsActive)
                .ToList();

            if (TryBuildCapacity(activeSections, "Aktif donem course section ortalamasi", out var activeCapacity))
            {
                recommendation = activeCapacity;
                ApplyCourseTypeCapacityAdjustments(recommendation, course);
                return recommendation;
            }
        }

        var historicalSections = context.AllSections
            .Where(section => section.CourseId == course.Id)
            .ToList();

        if (TryBuildCapacity(historicalSections, "Gecmis course section ortalamasi", out var historicalCapacity))
        {
            recommendation = historicalCapacity;
            ApplyCourseTypeCapacityAdjustments(recommendation, course);
            return recommendation;
        }

        if (course.DepartmentId != null && context.ActiveTerm != null)
        {
            var activeDepartmentSections = context.AllSections
                .Where(section => section.TermId == context.ActiveTerm.Id
                    && section.IsActive
                    && section.Course.DepartmentId == course.DepartmentId)
                .ToList();

            if (TryBuildCapacity(activeDepartmentSections, "Aktif donem department section ortalamasi", out var departmentActiveCapacity))
            {
                departmentActiveCapacity.Notes.Add("Bu ders icin dogrudan capacity verisi olmadigindan ayni bolumun aktif donem section kapasite ortalamasi kullanildi.");
                recommendation = departmentActiveCapacity;
                ApplyCourseTypeCapacityAdjustments(recommendation, course);
                return recommendation;
            }
        }

        if (course.DepartmentId != null)
        {
            var departmentSections = context.AllSections
                .Where(section => section.Course.DepartmentId == course.DepartmentId)
                .ToList();

            if (TryBuildCapacity(departmentSections, "Bolum geneli tarihsel section ortalamasi", out var departmentHistoricalCapacity))
            {
                departmentHistoricalCapacity.Notes.Add("Bu ders icin dogrudan capacity verisi olmadigindan ayni bolumun tarihsel section kapasite ortalamasi kullanildi.");
                recommendation = departmentHistoricalCapacity;
                ApplyCourseTypeCapacityAdjustments(recommendation, course);
                return recommendation;
            }
        }

        if (TryBuildCapacity(context.AllSections, "Sistem geneli tarihsel section ortalamasi", out var globalCapacity))
        {
            globalCapacity.Notes.Add("Bu ders ve bolum icin capacity verisi olmadigindan sistem geneli tarihsel section kapasite ortalamasi kullanildi.");
            recommendation = globalCapacity;
            ApplyCourseTypeCapacityAdjustments(recommendation, course);
            return recommendation;
        }

        return new CapacityRecommendation
        {
            EffectiveCapacity = 0,
            CapacityBasis = "Capacity verisi bulunamadi",
            Notes =
            {
                "Mevcut veya gecmis section/capacity verisi bulunamadigi icin onerilen section sayisi 0 donduruldu."
            }
        };
    }

    private static void ApplyCourseTypeCapacityAdjustments(CapacityRecommendation recommendation, Course course)
    {
        // Course type adjustments
        if (course.LabHours > 0 && course.TheoryHours == 0)
        {
            recommendation.EffectiveCapacity = Math.Min(recommendation.EffectiveCapacity, 30);
            recommendation.Notes.Add("Lab dersi: kapasite lab kapasitesiyle sınırlandırıldı (max 30).");
        }
        else if (course.LabHours > 0 && course.TheoryHours > 0)
        {
            recommendation.EffectiveCapacity = Math.Min(recommendation.EffectiveCapacity, 40);
            recommendation.Notes.Add("Karma ders (teori+lab): kapasite lab kısıtıyla dengelendi (max 40).");
        }

        if (course.TeachingMethod == TeachingMethod.Online)
        {
            recommendation.EffectiveCapacity = Math.Max(recommendation.EffectiveCapacity, 100);
            recommendation.Notes.Add("Online ders: kapasite fiziksel kısıtlara tabi değil.");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  HELPER METHODS
    // ═══════════════════════════════════════════════════════════════

    private async Task<decimal> GetEffectiveCgpaAsync(Student student, IDictionary<int, decimal> cgpaCache)
    {
        if (cgpaCache.TryGetValue(student.Id, out var cachedCgpa))
            return cachedCgpa;

        var effectiveCgpa = await _gpaCalculationService.CalculateCgpaAsync(student.Id);
        cgpaCache[student.Id] = effectiveCgpa;
        return effectiveCgpa;
    }

    private static bool TryBuildCapacity(
        IEnumerable<CourseSection> sections,
        string capacityBasis,
        out CapacityRecommendation recommendation)
    {
        var effectiveCapacities = sections
            .Select(section => ComputeEffectiveCapacity(section.Capacity, section.Classroom?.Capacity))
            .Where(capacity => capacity > 0)
            .ToList();

        if (effectiveCapacities.Count == 0)
        {
            recommendation = new CapacityRecommendation();
            return false;
        }

        recommendation = new CapacityRecommendation
        {
            EffectiveCapacity = Math.Max(1, (int)Math.Floor(effectiveCapacities.Average())),
            CapacityBasis = capacityBasis
        };

        return true;
    }

    private static int ComputeEffectiveCapacity(int sectionCapacity, int? classroomCapacity)
    {
        var normalizedSectionCapacity = Math.Max(0, sectionCapacity);
        if (classroomCapacity == null)
            return normalizedSectionCapacity;

        return Math.Min(normalizedSectionCapacity, Math.Max(0, classroomCapacity.Value));
    }

    private static int CalculateRecommendedSectionCount(int totalPredictedDemand, int effectiveCapacity)
    {
        if (totalPredictedDemand <= 0 || effectiveCapacity <= 0)
            return 0;

        return (int)Math.Ceiling(totalPredictedDemand / (decimal)effectiveCapacity);
    }

    private static Dictionary<int, HashSet<int>> BuildEquivalentCourseMap(IEnumerable<CourseEquivalency> equivalencyPairs)
    {
        var map = new Dictionary<int, HashSet<int>>();

        foreach (var equivalency in equivalencyPairs)
        {
            AddEquivalentCourse(map, equivalency.CourseId, equivalency.EquivalentCourseId);
            AddEquivalentCourse(map, equivalency.EquivalentCourseId, equivalency.CourseId);
        }

        return map;
    }

    private static void AddEquivalentCourse(Dictionary<int, HashSet<int>> map, int courseId, int equivalentCourseId)
    {
        if (!map.TryGetValue(courseId, out var equivalents))
        {
            equivalents = new HashSet<int>();
            map[courseId] = equivalents;
        }

        equivalents.Add(equivalentCourseId);
    }

    private static bool ArePrerequisitesMet(
        int courseId,
        IReadOnlySet<int> passedCourseIds,
        IReadOnlyDictionary<int, HashSet<int>> prerequisitesByCourseId,
        IReadOnlyDictionary<int, HashSet<int>> equivalentCourseIdsByCourseId)
    {
        if (!prerequisitesByCourseId.TryGetValue(courseId, out var prerequisiteIds) || prerequisiteIds.Count == 0)
            return true;

        foreach (var prerequisiteId in prerequisiteIds)
        {
            if (passedCourseIds.Contains(prerequisiteId))
                continue;

            if (equivalentCourseIdsByCourseId.TryGetValue(prerequisiteId, out var equivalentCourseIds)
                && equivalentCourseIds.Any(passedCourseIds.Contains))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static int CalculateCandidatePriority(int suggestedSemester, int currentSemester, decimal cgpa)
    {
        var semesterGap = currentSemester - suggestedSemester;
        var priority = semesterGap switch
        {
            >= 2 => 340 + (semesterGap * 15),
            1 => 320,
            0 => 300,
            -1 => 220,
            _ => 180
        };

        if (semesterGap > 0 && cgpa < 2.0m)
            priority += 20;

        if (semesterGap < 0)
        {
            priority += cgpa switch
            {
                >= 3.0m => 35,
                >= 2.5m => 15,
                _ => 0
            };
        }

        return priority;
    }

    private static void SelectCandidatesWithinEctsLimit(
        IEnumerable<CourseCandidate> candidates,
        ISet<int> regularCourseIds,
        ISet<int> behindScheduleCourseIds,
        ISet<int> upperCourseIds,
        ref int remainingEcts)
    {
        foreach (var candidate in candidates
                     .OrderByDescending(candidate => candidate.Priority)
                     .ThenBy(candidate => candidate.Ects)
                     .ThenBy(candidate => candidate.SuggestedSemester)
                     .ThenBy(candidate => candidate.CourseCode)
                     .ThenBy(candidate => candidate.CourseId))
        {
            if (candidate.Ects <= 0)
                continue;

            if (candidate.Ects > remainingEcts)
                continue;

            switch (candidate.Bucket)
            {
                case CourseCandidateBucket.BehindSchedule:
                    behindScheduleCourseIds.Add(candidate.CourseId);
                    break;
                case CourseCandidateBucket.UpperCourse:
                    upperCourseIds.Add(candidate.CourseId);
                    break;
                default:
                    regularCourseIds.Add(candidate.CourseId);
                    break;
            }

            remainingEcts -= candidate.Ects;
        }
    }

    private static int SumCourseEcts(IEnumerable<int> courseIds, IReadOnlyDictionary<int, Course> courses) =>
        courseIds
            .Distinct()
            .Sum(courseId => courses.TryGetValue(courseId, out var course) ? Math.Max(0, course.Ects) : 0);

    private static List<string> BuildGlobalNotes(AcademicTerm? activeTerm, int activeStudentCount, int eligibleProgramCount)
    {
        var notes = new List<string>
        {
            "Tahmin sadece aktif durumdaki, finansal blokesi olmayan ve aktif ana programi bulunan ogrenciler uzerinden hesaplandi.",
            "CGPA tabanli ECTS limitleri mevcut IGpaCalculationService uzerinden hesaplandi.",
            "Ayni ogrenci ayni ders icin yalnizca bir kez sayildi; kategori onceligi Retake > Regular Eligible > UpperClass/Overload olarak uygulandi.",
            "U notu, mevcut registration validation mantigiyla uyumlu olacak sekilde FF/NA ile ayni basarisiz kategoriye dahil edildi.",
            "Registration period ve section bazli schedule conflict kontrolleri talep tahminini yapisal ihtiyac bazli tutmak icin bilerek uygulanmadi.",
            "Aktif donemde Draft/PendingAdvisorApproval/Active durumundaki kayitlar mevcut talep olarak dahil edildi ve ogrencinin ECTS headroom'unu tuketti.",
            "Effective capacity classroom varsa min(section.Capacity, classroom.Capacity), yoksa section.Capacity olarak hesaplandi."
        };

        if (activeTerm == null)
            notes.Add("Aktif akademik donem bulunamadigi icin aktif section verisi capacity fallback'leriyle desteklendi.");

        if (eligibleProgramCount == 0)
            notes.Add("Aktif mufredat kaydi bulunamadigi icin tahminler yalnizca tekrar dersleri ve mevcut kayitlar uzerinden uretildi.");

        if (activeStudentCount == 0 || eligibleProgramCount == 0)
            notes.Add("Tahmin havuzunda uygun aktif ogrenci bulunamadigi icin sonuc sifir talep olarak hesaplandi.");

        return notes;
    }

    private static List<string> BuildDepartmentNotes(
        ForecastContextData context,
        IReadOnlyCollection<CourseDemandForecastDto> courseSummaries)
    {
        var notes = new List<string>(context.GlobalNotes)
        {
            "Department toplam talep degeri, course bazli tahminlerin toplami olarak hesaplandi; benzersiz ogrenci sayisi degildir."
        };

        if (courseSummaries.Count == 0)
            notes.Add("Bolum icin aktif mufredat, aktif section veya tahminlenen ogrenci talebi bulunamadi.");

        return notes.Distinct().ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    //  INTERNAL TYPES
    // ═══════════════════════════════════════════════════════════════

    private sealed class ForecastContextData
    {
        public AcademicTerm? ActiveTerm { get; init; }
        public required List<AcademicTerm> AllTerms { get; init; }
        public required Dictionary<int, AcademicTerm> TermsById { get; init; }
        public required Dictionary<int, Course> Courses { get; init; }
        public required List<CourseSection> AllSections { get; init; }
        public required List<StudentForecastSnapshot> StudentSnapshots { get; init; }
        public required HashSet<int> ActiveCurriculumCourseIds { get; init; }
        public required HashSet<int> ActiveTermSectionCourseIds { get; init; }
        public required Dictionary<int, Dictionary<int, int>> HistoricalEnrollmentsByCourse { get; init; }
        public required Dictionary<(int CourseId, int TermId), int> SectionCountByCourseTerm { get; init; }
        public required List<string> GlobalNotes { get; init; }
        public required Dictionary<int, List<ApprovalFeedbackRecord>> ApprovalFeedbackByCourse { get; init; }
        public Dictionary<int, decimal> ShowRateByCourse { get; set; } = new();
        public Dictionary<int, decimal> CohortGrowthByDepartment { get; set; } = new();
        public Dictionary<int, List<int>> ClassroomCapacitiesByFaculty { get; set; } = new();
        public Dictionary<int, int> DepartmentFacultyMap { get; set; } = new();
        public Dictionary<int, (decimal RuleWeight, decimal HistoricalWeight)> AdaptiveWeightsByCourse { get; set; } = new();

        public HashSet<int> PredictedDemandCourseIds =>
            StudentSnapshots
                .SelectMany(snapshot => snapshot.RetakeCourseIds
                    .Concat(snapshot.RegularCourseIds)
                    .Concat(snapshot.BehindScheduleCourseIds)
                    .Concat(snapshot.UpperCourseIds))
                .ToHashSet();
    }

    private sealed class StudentForecastSnapshot
    {
        public int StudentId { get; init; }
        public int DepartmentId { get; init; }
        public string DepartmentName { get; init; } = string.Empty;
        public string ProgramName { get; init; } = string.Empty;
        public HashSet<int> RetakeCourseIds { get; init; } = new();
        public HashSet<int> RetakeFFCourseIds { get; init; } = new();
        public HashSet<int> RetakeNACourseIds { get; init; } = new();
        public HashSet<int> RetakeUCourseIds { get; init; } = new();
        public HashSet<int> RegularCourseIds { get; init; } = new();
        public HashSet<int> BehindScheduleCourseIds { get; init; } = new();
        public HashSet<int> UpperCourseIds { get; init; } = new();
        public HashSet<int> EctsLimitedCourseIds { get; init; } = new();
    }

    private sealed record CourseCandidate(
        int CourseId,
        int Ects,
        int SuggestedSemester,
        string CourseCode,
        CourseCandidateBucket Bucket,
        int Priority);

    private enum CourseCandidateBucket
    {
        Regular,
        BehindSchedule,
        UpperCourse
    }

    private sealed class CapacityRecommendation
    {
        public int EffectiveCapacity { get; set; }
        public string CapacityBasis { get; set; } = string.Empty;
        public List<string> Notes { get; } = new();
    }

    private sealed class HistoricalEnrollmentRecord
    {
        public int CourseId { get; init; }
        public int TermId { get; init; }
        public int EnrollmentCount { get; init; }
    }

    private sealed class ApprovalFeedbackRecord
    {
        public int CourseId { get; init; }
        public int TermId { get; init; }
        public int RecommendedSectionCount { get; init; }
        public int ApprovedSectionCount { get; init; }
    }

    private sealed class ApprovalFeedbackResult
    {
        public int AdjustedSectionCount { get; init; }
        public List<string> Notes { get; } = new();

        public static ApprovalFeedbackResult NoChange(int sectionCount) => new()
        {
            AdjustedSectionCount = sectionCount
        };
    }
    private static List<HistoricalEnrollmentDto> BuildComparableHistoricalEnrollments(
        ForecastContextData context,
        List<HistoricalEnrollmentDto> historicalData)
    {
        if (context.ActiveTerm == null)
            return new List<HistoricalEnrollmentDto>(historicalData);

        return historicalData
            .Where(item =>
                context.TermsById.TryGetValue(item.TermId, out var term)
                && term.Semester == context.ActiveTerm.Semester)
            .ToList();
    }

    private static List<CrossDepartmentDemandDto> BuildCrossDepartmentDemand(
        ForecastContextData context,
        Course course,
        IEnumerable<int> predictedStudentIds)
    {
        var owningDepartmentId = course.DepartmentId;
        if (!owningDepartmentId.HasValue)
            return new List<CrossDepartmentDemandDto>();

        var predictedSet = predictedStudentIds.ToHashSet();
        if (predictedSet.Count == 0)
            return new List<CrossDepartmentDemandDto>();

        return context.StudentSnapshots
            .Where(snapshot =>
                predictedSet.Contains(snapshot.StudentId)
                && snapshot.DepartmentId > 0
                && snapshot.DepartmentId != owningDepartmentId.Value)
            .GroupBy(snapshot => new
            {
                snapshot.DepartmentId,
                snapshot.DepartmentName,
                snapshot.ProgramName
            })
            .Select(group => new CrossDepartmentDemandDto
            {
                DepartmentId = group.Key.DepartmentId,
                DepartmentName = group.Key.DepartmentName,
                ProgramName = group.Key.ProgramName,
                EstimatedStudentCount = group
                    .Select(snapshot => snapshot.StudentId)
                    .Distinct()
                    .Count()
            })
            .OrderByDescending(item => item.EstimatedStudentCount)
            .ThenBy(item => item.DepartmentName)
            .ThenBy(item => item.ProgramName)
            .ToList();
    }

    private static ApprovalFeedbackResult ApplyApprovalFeedback(
        ForecastContextData context,
        int courseId,
        int recommendedSectionCount,
        int totalPredictedDemand)
    {
        if (recommendedSectionCount <= 0 || totalPredictedDemand <= 0)
            return ApprovalFeedbackResult.NoChange(recommendedSectionCount);

        if (!context.ApprovalFeedbackByCourse.TryGetValue(courseId, out var history) || history.Count == 0)
            return ApprovalFeedbackResult.NoChange(recommendedSectionCount);

        var weightedDeltas = new List<(double Weight, int Delta)>();

        foreach (var record in history)
        {
            if (!context.TermsById.TryGetValue(record.TermId, out var term))
                continue;

            var delta = record.ApprovedSectionCount - record.RecommendedSectionCount;
            if (delta == 0)
                continue;

            var weight = 1.0;
            if (context.ActiveTerm != null)
            {
                if (term.Semester == context.ActiveTerm.Semester)
                    weight *= 1.6;

                var yearGap = Math.Max(0, context.ActiveTerm.Year - term.Year);
                weight *= yearGap switch
                {
                    0 => 1.4,
                    1 => 1.2,
                    2 => 1.0,
                    _ => 0.8
                };
            }

            weightedDeltas.Add((weight, delta));
        }

        if (weightedDeltas.Count == 0)
            return ApprovalFeedbackResult.NoChange(recommendedSectionCount);

        var totalWeight = weightedDeltas.Sum(item => item.Weight);
        var weightedDelta = weightedDeltas.Sum(item => item.Weight * item.Delta) / totalWeight;
        var dampedDelta = weightedDelta >= 0
            ? Math.Min(1.5, weightedDelta * 0.75)
            : Math.Max(-1.0, weightedDelta * 0.50);
        var roundedDelta = (int)Math.Round(dampedDelta, MidpointRounding.AwayFromZero);

        if (roundedDelta == 0)
            return ApprovalFeedbackResult.NoChange(recommendedSectionCount);

        var adjusted = Math.Max(1, recommendedSectionCount + roundedDelta);
        if (adjusted == recommendedSectionCount)
            return ApprovalFeedbackResult.NoChange(recommendedSectionCount);

        var note = roundedDelta > 0
            ? $"Gecmis onay kararlarindaki yukari yonlu sapma nedeniyle onerilen sube sayisi {adjusted} olarak yukseltilmistir."
            : $"Gecmis onay kararlarindaki asagi yonlu sapma nedeniyle onerilen sube sayisi {adjusted} olarak dengelenmistir.";

        return new ApprovalFeedbackResult
        {
            AdjustedSectionCount = adjusted,
            Notes = { note }
        };
    }
}
