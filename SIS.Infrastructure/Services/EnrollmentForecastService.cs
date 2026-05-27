using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIS.Application.EnrollmentForecast.DTOs;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Enrollment forecast — ders/bölüm tahmin + simülasyon + kalibrasyon. Partial class:
///   - EnrollmentForecastService.cs            (ctor, alanlar, public read API'ler, kalibrasyon, simülasyon, küçük helper'lar)
///   - EnrollmentForecastService.Context.cs    (ForecastContextData inşası — en büyük blok)
///   - EnrollmentForecastService.Snapshot.cs   (BuildStudentSnapshotAsync — öğrenci-bazlı snapshot)
///   - EnrollmentForecastService.Builders.cs   (CourseDemandForecast + CapacityRecommendation + sub-helpers)
/// </summary>
public partial class EnrollmentForecastService : IEnrollmentForecastService, IEnrollmentForecastCacheInvalidator
{
    private const string ForecastContextCacheKey = "ForecastContext";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _context;
    private readonly IGpaCalculationService _gpaCalculationService;
    private readonly IMemoryCache _cache;
    private readonly ISystemConfigProvider _config;
    private readonly IDateTimeProvider _clock;
    private readonly IEctsLimitResolver _ectsLimitResolver;

    public EnrollmentForecastService(AppDbContext context, IGpaCalculationService gpaCalculationService, IMemoryCache cache, IDateTimeProvider clock, ISystemConfigProvider? config = null, IEctsLimitResolver? ectsLimitResolver = null)
    {
        _context = context;
        _gpaCalculationService = gpaCalculationService;
        _cache = cache;
        _clock = clock;
        _config = config ?? new NullSystemConfigProvider();
        _ectsLimitResolver = ectsLimitResolver ?? new EctsLimitResolver(_config);
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
        // Bölüm kapsamlı cache'leri de temizle
        if (_cache is MemoryCache mc)
            mc.Compact(1.0); // Tüm forecast cache'leri dahil tüm entry'leri temizler
    }

    public async Task<DepartmentForecastSummaryDto?> GetDepartmentSummaryAsync(int departmentId)
    {
        var department = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == departmentId);

        if (department == null)
            return null;

        var forecastContext = await GetOrBuildForecastContextAsync(departmentId);

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
        var term = await _context.AcademicTerms.AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");

        if (!term.IsActive(_clock.UtcNow) || !term.IsAddDropOpen(_clock.UtcNow))
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

    public async Task<CourseDemandForecastDto?> SimulateCourseDemandAsync(int courseId, ForecastSimulationParams parameters)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course == null)
            return null;

        var forecastContext = await GetOrBuildForecastContextAsync();
        var forecast = BuildCourseDemandForecast(forecastContext, course);

        // Override'ları uygula
        if (parameters.OverrideExpectedEnrollment.HasValue)
        {
            forecast.TotalPredictedDemand = parameters.OverrideExpectedEnrollment.Value;
            forecast.GrossPredictedDemand = parameters.OverrideExpectedEnrollment.Value;
            forecast.Trace?.Steps.Add(new ForecastTraceStepDto
            {
                StepName = "SimulationOverride",
                Description = $"Talep manuel olarak {parameters.OverrideExpectedEnrollment.Value} olarak ayarlandı",
                OutputValue = parameters.OverrideExpectedEnrollment.Value
            });
        }

        if (parameters.OverrideShowRate.HasValue)
        {
            var newShowRate = parameters.OverrideShowRate.Value;
            var originalRuleBasedTotal = forecast.PredictedEligibleCount + forecast.PredictedRetakeCount + forecast.PredictedOverloadOrUpperClassCount;
            var adjusted = (int)Math.Round(originalRuleBasedTotal * newShowRate);
            forecast.ShowRate = newShowRate;
            forecast.Trace?.Steps.Add(new ForecastTraceStepDto
            {
                StepName = "SimulationShowRate",
                Description = $"Show rate %{newShowRate * 100:F0} olarak override edildi",
                InputValue = originalRuleBasedTotal, OutputValue = adjusted,
                Factor = newShowRate
            });
        }

        if (parameters.OverrideAttritionRate.HasValue)
        {
            var gross = forecast.GrossPredictedDemand;
            var newRate = parameters.OverrideAttritionRate.Value;
            forecast.AttritionRate = newRate;
            forecast.AttritionMethod = "Simülasyon override";
            forecast.TotalPredictedDemand = (int)Math.Round(gross * (1 - newRate));
            forecast.Trace?.Steps.Add(new ForecastTraceStepDto
            {
                StepName = "SimulationAttrition",
                Description = $"Attrition %{newRate * 100:F1} olarak override edildi",
                InputValue = gross, OutputValue = forecast.TotalPredictedDemand,
                Factor = 1 - newRate
            });
        }

        var effectiveCapacity = parameters.OverrideCapacityPerSection
            ?? forecast.SectionDistribution.EffectiveCapacityPerSection;

        if (parameters.OverrideSectionCount.HasValue)
        {
            forecast.RecommendedSectionCount = parameters.OverrideSectionCount.Value;
        }
        else if (parameters.OverrideCapacityPerSection.HasValue)
        {
            forecast.RecommendedSectionCount = effectiveCapacity > 0
                ? (int)Math.Ceiling((decimal)forecast.TotalPredictedDemand / effectiveCapacity)
                : 0;
        }

        // Section distribution'ı yeniden hesapla
        forecast.SectionDistribution = BuildSectionDistribution(
            forecast.TotalPredictedDemand,
            forecast.RecommendedSectionCount,
            effectiveCapacity);

        forecast.IsSimulation = true;
        forecast.SimulationParams = parameters;

        return forecast;
    }

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

        // Seçmeli dağılım verileri
        public Dictionary<int, List<ElectivePoolContext>> ElectivePoolsByCurriculum { get; set; } = new();
        public Dictionary<int, decimal> ElectivePopularityScores { get; set; } = new();

        // Attrition (drop/withdrawal) verileri
        public Dictionary<int, decimal> AttritionRateByCourse { get; set; } = new();
        public Dictionary<int, decimal> AttritionRateByDepartment { get; set; } = new();

        // Ardışık ders bağımlılığı
        public Dictionary<int, int> CurrentTermEnrollmentByCourse { get; set; } = new();
        public Dictionary<int, HashSet<int>> PrerequisitesByCourseId { get; set; } = new();

        // Ders volatilite skoru
        public Dictionary<int, decimal> VolatilityScoreByCourse { get; set; } = new();

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

        /// <summary>Seçmeli dersler için fraksiyonel talep ağırlığı (zorunlu dersler burada yer almaz, implicit 1.0)</summary>
        public Dictionary<int, decimal> ElectiveDemandWeights { get; init; } = new();
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

    private sealed class ElectivePoolContext
    {
        public int ElectiveGroupId { get; init; }
        public string ElectiveGroupName { get; init; } = string.Empty;
        public int RequiredCourseCount { get; init; }
        public int RequiredEcts { get; init; }
        public int SuggestedSemesterStart { get; init; }
        public int SuggestedSemesterEnd { get; init; }
        public HashSet<int> CourseIdsInGroup { get; init; } = new();
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
                && term.TermType == context.ActiveTerm.TermType)
            .ToList();
    }

    private static (int NetPrediction, decimal AttritionRate, string Method) ApplyAttritionAdjustment(
        ForecastContextData context, Course course, int grossPrediction)
    {
        const decimal systemDefault = 0.08m;

        // 1. Ders bazlı attrition
        if (context.AttritionRateByCourse.TryGetValue(course.Id, out var courseRate))
        {
            var net = (int)Math.Round(grossPrediction * (1 - courseRate));
            return (Math.Max(0, net), courseRate, "Ders bazlı geçmiş veri");
        }

        // 2. Bölüm bazlı fallback
        if (course.DepartmentId.HasValue &&
            context.AttritionRateByDepartment.TryGetValue(course.DepartmentId.Value, out var deptRate))
        {
            var net = (int)Math.Round(grossPrediction * (1 - deptRate));
            return (Math.Max(0, net), deptRate, "Bölüm ortalaması");
        }

        // 3. Sistem varsayılanı
        var defaultNet = (int)Math.Round(grossPrediction * (1 - systemDefault));
        return (Math.Max(0, defaultNet), systemDefault, "Sistem varsayılanı (%8)");
    }

    private static ElectiveDistributionDto? BuildElectiveDistribution(
        ForecastContextData context,
        Course course,
        IReadOnlySet<int> predictedStudentIds)
    {
        // Ders herhangi bir seçmeli havuzda mı?
        ElectivePoolContext? matchedPool = null;
        foreach (var pools in context.ElectivePoolsByCurriculum.Values)
        {
            matchedPool = pools.FirstOrDefault(p => p.CourseIdsInGroup.Contains(course.Id));
            if (matchedPool != null) break;
        }

        if (matchedPool == null) return null;

        var eligibleInPool = predictedStudentIds.Count;
        var popularity = context.ElectivePopularityScores.GetValueOrDefault(course.Id, 0m);

        // Tüm havuzdaki toplam dağıtılmış talep
        var totalDistributed = context.StudentSnapshots
            .Where(s => predictedStudentIds.Contains(s.StudentId))
            .Sum(s => s.ElectiveDemandWeights.GetValueOrDefault(course.Id, 0m));

        return new ElectiveDistributionDto
        {
            ElectiveGroupId = matchedPool.ElectiveGroupId,
            ElectiveGroupName = matchedPool.ElectiveGroupName,
            TotalCoursesInPool = matchedPool.CourseIdsInGroup.Count,
            RequiredSelections = matchedPool.RequiredCourseCount,
            PopularityScore = Math.Round(popularity, 3),
            EligibleStudentsInPool = eligibleInPool,
            DistributedDemandShare = Math.Round(totalDistributed, 1)
        };
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
                if (term.TermType == context.ActiveTerm.TermType)
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
