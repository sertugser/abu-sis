using Microsoft.EntityFrameworkCore;
using SIS.Application.EnrollmentForecast.DTOs;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class EnrollmentForecastService : IEnrollmentForecastService
{
    private readonly AppDbContext _context;
    private readonly IGpaCalculationService _gpaCalculationService;

    public EnrollmentForecastService(AppDbContext context, IGpaCalculationService gpaCalculationService)
    {
        _context = context;
        _gpaCalculationService = gpaCalculationService;
    }

    public async Task<CourseDemandForecastDto?> GetCourseDemandForecastAsync(int courseId)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course == null)
            return null;

        var forecastContext = await BuildForecastContextAsync();
        return BuildCourseDemandForecast(forecastContext, course);
    }

    public async Task<DepartmentForecastSummaryDto?> GetDepartmentSummaryAsync(int departmentId)
    {
        var department = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == departmentId);

        if (department == null)
            return null;

        var forecastContext = await BuildForecastContextAsync();

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
                    .Count(s => s.SectionDistribution.Warnings.Any(w => w.Contains("Kapasite"))),
                CoursesWithExcessCapacity = courseSummaries
                    .Count(s => s.SectionDistribution.UtilizationRate < 0.50m && s.SectionDistribution.UtilizationRate > 0)
            }
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  FORECAST CONTEXT BUILDER
    // ═══════════════════════════════════════════════════════════════

    private async Task<ForecastContextData> BuildForecastContextAsync()
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
            GlobalNotes = BuildGlobalNotes(activeTerm, activeStudents.Count, activeMainPrograms.Count)
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
        var ectsLimit = RegistrationRulePolicy.GetEctsLimit(cgpa);
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

        var regularCandidates = new List<CourseCandidate>();
        var behindScheduleCandidates = new List<CourseCandidate>();
        var upperCandidates = new List<CourseCandidate>();

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

                if (RegistrationRulePolicy.HasLowCgpaRestriction(cgpa, completedSemesters)
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
                    course.Code);

                if (curriculumCourse.SuggestedSemester > currentSemester)
                    upperCandidates.Add(candidate);
                else if (curriculumCourse.SuggestedSemester < currentSemester)
                    behindScheduleCandidates.Add(candidate);
                else
                    regularCandidates.Add(candidate);
            }
        }

        // Track ECTS-limited courses before selecting
        var ectsBeforeRegular = remainingEcts;
        SelectCandidatesWithinEctsLimit(regularCandidates, regularCourseIds, ref remainingEcts);
        SelectCandidatesWithinEctsLimit(behindScheduleCandidates, behindScheduleCourseIds, ref remainingEcts);
        SelectCandidatesWithinEctsLimit(upperCandidates, upperCourseIds, ref remainingEcts);

        // Track candidates that were eligible but couldn't fit
        foreach (var candidate in regularCandidates.Concat(behindScheduleCandidates).Concat(upperCandidates))
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

        // --- Historical analysis ---
        var historicalData = BuildHistoricalEnrollments(context, course.Id);
        var historicalCounts = historicalData.Select(h => h.ActualEnrollment).ToList();

        // --- Hybrid blending ---
        var blendedPrediction = BlendPrediction(ruleBasedTotal, historicalCounts);

        // --- Trend adjustment ---
        var finalPrediction = ApplyTrendAdjustment(blendedPrediction, historicalCounts);

        // --- Confidence interval ---
        var confidence = CalculateConfidence(finalPrediction, historicalCounts);

        // --- Section distribution ---
        var capacityRecommendation = BuildCapacityRecommendation(context, course);
        var recommendedSections = CalculateRecommendedSectionCount(finalPrediction, capacityRecommendation.EffectiveCapacity);
        var sectionDistribution = BuildSectionDistribution(
            finalPrediction, recommendedSections, capacityRecommendation.EffectiveCapacity);

        // --- Notes ---
        var notes = new List<string>(context.GlobalNotes);

        if (!context.ActiveCurriculumCourseIds.Contains(course.Id))
            notes.Add("Bu ders aktif mufredatlarda yer almiyor; tahmin agirlikla tekrarlar ve mevcut aktif kayitlar uzerinden uretilir.");

        if (!context.ActiveTermSectionCourseIds.Contains(course.Id))
            notes.Add("Bu ders icin aktif donemde section bulunmuyor; talep latent ihtiyac olarak hesaplandi.");

        if (historicalCounts.Count >= 3)
            notes.Add($"Tahmin, {historicalCounts.Count} donemlik gecmis veri ile harmanlanarak uretildi (agirlik: %60 kural, %40 tarihsel).");
        else if (historicalCounts.Count >= 1)
            notes.Add($"Tahmin, {historicalCounts.Count} donemlik gecmis veri ile harmanlanarak uretildi (agirlik: %80 kural, %20 tarihsel).");
        else
            notes.Add("Gecmis donem verisi bulunamadigi icin tahmin tamamen kural tabanli uretildi.");

        notes.AddRange(capacityRecommendation.Notes);

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
            SectionDistribution = sectionDistribution
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

    private static int BlendPrediction(int ruleBasedTotal, List<int> historicalCounts)
    {
        if (historicalCounts.Count == 0)
            return ruleBasedTotal;

        var historicalAvg = historicalCounts.Average();

        double blended;
        if (historicalCounts.Count >= 3)
            blended = 0.60 * ruleBasedTotal + 0.40 * historicalAvg;
        else
            blended = 0.80 * ruleBasedTotal + 0.20 * historicalAvg;

        return (int)Math.Round(blended);
    }

    private static int ApplyTrendAdjustment(int prediction, List<int> historicalCounts)
    {
        if (historicalCounts.Count < 2)
            return prediction;

        var recentCount = Math.Min(2, historicalCounts.Count);
        var recent = historicalCounts.TakeLast(recentCount).Average();
        var olderItems = historicalCounts.Take(historicalCounts.Count - recentCount).Select(x => (double)x).ToList();
        var older = olderItems.Count > 0 ? olderItems.Average() : recent;

        if (older <= 0)
            return prediction;

        var trendFactor = Math.Clamp(recent / older, 0.80, 1.20);
        return (int)Math.Round(prediction * trendFactor);
    }

    private static ConfidenceRangeDto CalculateConfidence(int prediction, List<int> historicalCounts)
    {
        if (historicalCounts.Count >= 2)
        {
            var mean = historicalCounts.Average();
            var variance = historicalCounts.Sum(x => Math.Pow(x - mean, 2)) / historicalCounts.Count;
            var stdDev = Math.Sqrt(variance);
            var margin = (int)Math.Ceiling(stdDev * 1.96);

            return new ConfidenceRangeDto
            {
                LowerBound = Math.Max(0, prediction - margin),
                UpperBound = prediction + margin,
                Margin = margin,
                Method = $"Historical StdDev ({historicalCounts.Count} donem)"
            };
        }

        var ruleOfThumbMargin = Math.Max(5, (int)Math.Ceiling(prediction * 0.15));
        return new ConfidenceRangeDto
        {
            LowerBound = Math.Max(0, prediction - ruleOfThumbMargin),
            UpperBound = prediction + ruleOfThumbMargin,
            Margin = ruleOfThumbMargin,
            Method = "Rule-of-Thumb (±%15, min ±5)"
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
        if (context.ActiveTerm != null)
        {
            var activeSections = context.AllSections
                .Where(section => section.CourseId == course.Id
                    && section.TermId == context.ActiveTerm.Id
                    && section.IsActive)
                .ToList();

            if (TryBuildCapacity(activeSections, "Aktif donem course section ortalamasi", out var activeCapacity))
                return activeCapacity;
        }

        var historicalSections = context.AllSections
            .Where(section => section.CourseId == course.Id)
            .ToList();

        if (TryBuildCapacity(historicalSections, "Gecmis course section ortalamasi", out var historicalCapacity))
            return historicalCapacity;

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
                return departmentActiveCapacity;
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
                return departmentHistoricalCapacity;
            }
        }

        if (TryBuildCapacity(context.AllSections, "Sistem geneli tarihsel section ortalamasi", out var globalCapacity))
        {
            globalCapacity.Notes.Add("Bu ders ve bolum icin capacity verisi olmadigindan sistem geneli tarihsel section kapasite ortalamasi kullanildi.");
            return globalCapacity;
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

    private static void SelectCandidatesWithinEctsLimit(
        IEnumerable<CourseCandidate> candidates,
        ISet<int> selectedCourseIds,
        ref int remainingEcts)
    {
        foreach (var candidate in candidates
                     .OrderBy(candidate => candidate.SuggestedSemester)
                     .ThenBy(candidate => candidate.CourseCode)
                     .ThenBy(candidate => candidate.CourseId))
        {
            if (candidate.Ects <= 0)
                continue;

            if (candidate.Ects > remainingEcts)
                continue;

            selectedCourseIds.Add(candidate.CourseId);
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
        string CourseCode);

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
}
