using Microsoft.EntityFrameworkCore;
using SIS.Application.EnrollmentForecast.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

/// <summary>
/// EnrollmentForecastService — ders talep + kapasite öneri builder'ları.
/// Tahmin algoritması (BlendPrediction, ApplyTrendAdjustment, CalculateConfidence)
/// + tarihi kayıt + section dağılımı + kapasite önerisi mantığı.
/// </summary>
public partial class EnrollmentForecastService
{
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

        // Fraksiyonel talep hesabı: seçmeli dersler için ağırlıklı, zorunlu dersler 1.0
        var snapshotByStudentId = context.StudentSnapshots.ToDictionary(s => s.StudentId);
        decimal WeightOf(int studentId) =>
            snapshotByStudentId.TryGetValue(studentId, out var snap)
                ? snap.ElectiveDemandWeights.GetValueOrDefault(course.Id, 1.0m)
                : 1.0m;

        var retakeDemand = retakeStudents.Sum(WeightOf);
        var regularDemand = regularStudents.Sum(WeightOf);
        var behindDemand = behindScheduleStudents.Sum(WeightOf);
        var upperDemand = upperStudents.Sum(WeightOf);

        var ruleBasedTotal = (int)Math.Round(retakeDemand + regularDemand + behindDemand + upperDemand);
        var predictedStudentIds = retakeStudents
            .Concat(regularStudents)
            .Concat(behindScheduleStudents)
            .Concat(upperStudents)
            .ToHashSet();

        // --- Trace builder ---
        var trace = new ForecastTraceDto();
        trace.Steps.Add(new ForecastTraceStepDto
        {
            StepName = "RuleBasedTotal",
            Description = $"Kural tabanlı toplam: {(int)Math.Round(regularDemand)} regular + {(int)Math.Round(behindDemand)} behind + {(int)Math.Round(upperDemand)} upper + {(int)Math.Round(retakeDemand)} retake",
            InputValue = 0, OutputValue = ruleBasedTotal
        });

        // --- Show rate adjustment ---
        var showRate = context.ShowRateByCourse.GetValueOrDefault(course.Id, 0.90m);
        var adjustedRuleBasedTotal = (int)Math.Round(ruleBasedTotal * showRate);
        trace.Steps.Add(new ForecastTraceStepDto
        {
            StepName = "ShowRate",
            Description = $"Show rate düzeltmesi (%{showRate * 100:F0})",
            InputValue = ruleBasedTotal, OutputValue = adjustedRuleBasedTotal,
            Factor = showRate,
            DataSource = context.ShowRateByCourse.ContainsKey(course.Id) ? "Kalibrasyon verisi" : "Varsayılan (%90)"
        });

        // --- Volatility score ---
        var volatility = context.VolatilityScoreByCourse.GetValueOrDefault(course.Id, 0.15m);
        var volatilityLevel = volatility switch { < 0.10m => "Stable", > 0.25m => "Volatile", _ => "Moderate" };

        // --- Cohort growth for courses ---
        var preGrowthTotal = adjustedRuleBasedTotal;
        if (course.DepartmentId.HasValue)
        {
            var cohortGrowth = context.CohortGrowthByDepartment.GetValueOrDefault(course.DepartmentId.Value, 1.0m);
            if (cohortGrowth != 1.0m)
            {
                // Volatiliteye göre adaptif dampening
                var dampening = volatility switch { < 0.10m => 0.3m, > 0.25m => 0.7m, _ => 0.5m };
                var cohortAdj = 1.0m + (cohortGrowth - 1.0m) * dampening;
                adjustedRuleBasedTotal = (int)Math.Round(adjustedRuleBasedTotal * cohortAdj);
                trace.Steps.Add(new ForecastTraceStepDto
                {
                    StepName = "CohortGrowth",
                    Description = $"Kohort büyüme düzeltmesi (ham: {cohortGrowth:F2}, dampenli: {cohortAdj:F2})",
                    InputValue = preGrowthTotal, OutputValue = adjustedRuleBasedTotal,
                    Factor = cohortAdj
                });
            }
        }

        // --- Historical analysis ---
        var historicalData = BuildHistoricalEnrollments(context, course.Id);
        var historicalCounts = historicalData.Select(h => h.ActualEnrollment).ToList();
        var comparableHistoricalData = BuildComparableHistoricalEnrollments(context, historicalData);
        var comparableHistoricalCounts = comparableHistoricalData.Select(h => h.ActualEnrollment).ToList();

        // --- Hybrid blending ---
        var adaptiveRuleWeight = context.AdaptiveWeightsByCourse.TryGetValue(course.Id, out var aw) ? (decimal?)aw.RuleWeight : null;
        var effectiveRuleWeight = adaptiveRuleWeight ?? (historicalCounts.Count >= 3 ? 0.55m : 0.75m);
        var blendedPrediction = BlendPrediction(adjustedRuleBasedTotal, historicalCounts, comparableHistoricalCounts, adaptiveRuleWeight);
        trace.Steps.Add(new ForecastTraceStepDto
        {
            StepName = "HistoricalBlend",
            Description = $"Kural (%{effectiveRuleWeight * 100:F0}) + Tarihsel (%{(1 - effectiveRuleWeight) * 100:F0}) harmanlama",
            InputValue = adjustedRuleBasedTotal, OutputValue = blendedPrediction,
            Factor = effectiveRuleWeight,
            DataSource = $"{historicalCounts.Count} dönem ({comparableHistoricalCounts.Count} karşılaştırılabilir)"
        });
        trace.FinalRuleWeight = effectiveRuleWeight;
        trace.FinalHistoricalWeight = 1 - effectiveRuleWeight;

        // --- Trend adjustment ---
        var grossPrediction = ApplyTrendAdjustment(blendedPrediction, historicalCounts, comparableHistoricalCounts, volatility);
        if (grossPrediction != blendedPrediction)
        {
            trace.Steps.Add(new ForecastTraceStepDto
            {
                StepName = "TrendAdjustment",
                Description = "Son dönemlerin ağırlıklı trend düzeltmesi",
                InputValue = blendedPrediction, OutputValue = grossPrediction,
                Factor = blendedPrediction > 0 ? Math.Round((decimal)grossPrediction / blendedPrediction, 3) : 1m
            });
        }

        // --- Sequential dependency adjustment ---
        var sequentialDeps = new List<SequentialDependencyDto>();
        var preSeqPrediction = grossPrediction;
        if (context.PrerequisitesByCourseId.TryGetValue(course.Id, out var prereqIds) && prereqIds.Count > 0)
        {
            var seqFactors = new List<decimal>();
            foreach (var prereqId in prereqIds)
            {
                if (!context.CurrentTermEnrollmentByCourse.TryGetValue(prereqId, out var currentPrereqEnroll))
                    continue;
                if (!context.Courses.TryGetValue(prereqId, out var prereqCourse))
                    continue;

                // Karşılaştırılabilir dönemlerdeki önkoşul ortalaması
                var prereqHistorical = context.HistoricalEnrollmentsByCourse.GetValueOrDefault(prereqId);
                if (prereqHistorical == null || prereqHistorical.Count == 0) continue;

                var comparablePrereqCounts = context.ActiveTerm != null
                    ? prereqHistorical
                        .Where(kv => context.TermsById.TryGetValue(kv.Key, out var t) && t.TermType == context.ActiveTerm.TermType)
                        .Select(kv => kv.Value)
                        .ToList()
                    : prereqHistorical.Values.ToList();

                if (comparablePrereqCounts.Count == 0) continue;
                var historicalAvg = (int)Math.Round(comparablePrereqCounts.Average());
                if (historicalAvg <= 0) continue;

                var ratio = (decimal)currentPrereqEnroll / historicalAvg;
                var rawDelta = ratio - 1.0m; // +0.20 = %20 artış
                var dampenedDelta = rawDelta * 0.6m; // %60 dampening
                var clampedFactor = 1.0m + Math.Clamp(dampenedDelta, -0.25m, 0.25m);

                seqFactors.Add(clampedFactor);
                sequentialDeps.Add(new SequentialDependencyDto
                {
                    PrerequisiteCourseId = prereqId,
                    PrerequisiteCourseCode = prereqCourse.Code,
                    CurrentPrereqEnrollment = currentPrereqEnroll,
                    HistoricalPrereqAverage = historicalAvg,
                    AdjustmentFactor = Math.Round(clampedFactor, 3)
                });
            }

            if (seqFactors.Count > 0)
            {
                var avgFactor = seqFactors.Average();
                grossPrediction = (int)Math.Round(grossPrediction * avgFactor);
                trace.Steps.Add(new ForecastTraceStepDto
                {
                    StepName = "SequentialDependency",
                    Description = $"{seqFactors.Count} önkoşul dersinin mevcut dönem kayıt etkisi",
                    InputValue = preSeqPrediction, OutputValue = grossPrediction,
                    Factor = Math.Round(avgFactor, 3),
                    DataSource = string.Join(", ", sequentialDeps.Select(d => d.PrerequisiteCourseCode))
                });
            }
        }

        // --- Attrition adjustment (gross → net) ---
        var (finalPrediction, attritionRate, attritionMethod) = ApplyAttritionAdjustment(context, course, grossPrediction);
        if (attritionRate > 0)
        {
            trace.Steps.Add(new ForecastTraceStepDto
            {
                StepName = "AttritionAdjustment",
                Description = $"Bırakma/çekilme düzeltmesi (%{attritionRate * 100:F1})",
                InputValue = grossPrediction, OutputValue = finalPrediction,
                Factor = 1 - attritionRate,
                DataSource = attritionMethod
            });
        }

        // --- Confidence interval ---
        var confidence = CalculateConfidence(finalPrediction, comparableHistoricalCounts, volatility);

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
            PredictedEligibleCount = (int)Math.Round(regularDemand + behindDemand),
            PredictedRetakeCount = (int)Math.Round(retakeDemand),
            PredictedOverloadOrUpperClassCount = (int)Math.Round(upperDemand),
            TotalPredictedDemand = finalPrediction,
            RecommendedSectionCount = recommendedSections,
            CapacityBasis = capacityRecommendation.CapacityBasis,
            ComparableTermEnrollment = comparableTermEnrollment,
            Notes = notes.Distinct().ToList(),

            // Enhanced fields
            DemandBreakdown = new DemandBreakdownDto
            {
                RetakeFF = (int)Math.Round(retakeFFStudents.Sum(WeightOf)),
                RetakeNA = (int)Math.Round(retakeNAStudents.Sum(WeightOf)),
                RetakeU = (int)Math.Round(retakeUStudents.Sum(WeightOf)),
                RegularOnSchedule = (int)Math.Round(regularDemand),
                BehindSchedule = (int)Math.Round(behindDemand),
                UpperCourse = (int)Math.Round(upperDemand),
                EctsLimited = ectsLimitedStudents.Count
            },
            HistoricalEnrollments = historicalData,
            Confidence = confidence,
            SectionDistribution = sectionDistribution,
            CrossDepartmentDemand = crossDepartmentDemand,
            ShowRate = showRate,
            ElectiveDistribution = BuildElectiveDistribution(context, course, predictedStudentIds),

            // Faz 2: Attrition + Trace
            GrossPredictedDemand = grossPrediction,
            AttritionRate = attritionRate,
            AttritionMethod = attritionMethod,
            Trace = trace,
            SequentialDependencies = sequentialDeps,
            VolatilityScore = volatility,
            VolatilityLevel = volatilityLevel
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
            context.TermsById.TryGetValue(kv.Key, out var t) ? t.Year * 10 + (int)t.TermType : kv.Key))
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

    private static int ApplyTrendAdjustment(int prediction, List<int> historicalCounts, List<int> comparableHistoricalCounts, decimal volatility = 0.15m)
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

        // Volatiliteye göre adaptif clamp aralığı
        var (clampMin, clampMax) = volatility switch
        {
            < 0.10m => (0.92, 1.10),   // Stabil ders: dar aralık
            > 0.25m => (0.75, 1.35),   // Volatil ders: geniş aralık
            _ => (0.85, 1.20)           // Orta: mevcut davranış
        };

        var trendFactor = Math.Clamp(weightedAvg / simpleAvg, clampMin, clampMax);
        return (int)Math.Round(prediction * trendFactor);
    }

    private static ConfidenceRangeDto CalculateConfidence(int prediction, List<int> comparableHistoricalCounts, decimal volatility = 0.15m)
    {
        var recent = comparableHistoricalCounts.TakeLast(4).ToList();

        if (recent.Count >= 2)
        {
            var mean = recent.Average();
            var variance = recent.Sum(x => Math.Pow(x - mean, 2)) / recent.Count;
            var stdDev = Math.Sqrt(variance);

            // Volatiliteye göre z-score ayarı
            var zScore = volatility switch
            {
                < 0.10m => 1.65,    // Stabil: %90 güven
                > 0.25m => 2.33,    // Volatil: %99 güven
                _ => 1.96           // Orta: %95 güven
            };

            var margin = (int)Math.Ceiling(stdDev * zScore);
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
                    && section.Status == CourseSectionStatus.Active)
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
                    && section.Status == CourseSectionStatus.Active
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

}
