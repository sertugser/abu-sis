namespace SIS.Application.EnrollmentForecast.DTOs;

public class CourseDemandForecastDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int PredictedEligibleCount { get; set; }
    public int PredictedRetakeCount { get; set; }
    public int PredictedOverloadOrUpperClassCount { get; set; }
    public int TotalPredictedDemand { get; set; }
    public int RecommendedSectionCount { get; set; }
    public string CapacityBasis { get; set; } = string.Empty;
    public int? ComparableTermEnrollment { get; set; }
    public List<string> Notes { get; set; } = new();

    // --- Enhanced fields ---

    /// <summary>Detayli talep kirilimi (FF/NA/U/regular/behindSchedule/upper/ectsLimited)</summary>
    public DemandBreakdownDto DemandBreakdown { get; set; } = new();

    /// <summary>Gecmis donem gercek kayit sayilari</summary>
    public List<HistoricalEnrollmentDto> HistoricalEnrollments { get; set; } = new();

    /// <summary>Tahmin guven araligi</summary>
    public ConfidenceRangeDto Confidence { get; set; } = new();

    /// <summary>Sube bazli ogrenci dagilim onerisi</summary>
    public SectionDistributionDto SectionDistribution { get; set; } = new();

    /// <summary>Diger bolum/programlardan gelen gercek tahmini talep kirilimi</summary>
    public List<CrossDepartmentDemandDto> CrossDepartmentDemand { get; set; } = new();

    public decimal ShowRate { get; set; }

    /// <summary>Seçmeli ders havuz dağılım bilgisi (zorunlu dersler için null)</summary>
    public ElectiveDistributionDto? ElectiveDistribution { get; set; }

    // --- Faz 2: Attrition + Trace ---

    /// <summary>Brüt talep tahmini (drop/withdrawal düşülmeden önce)</summary>
    public int GrossPredictedDemand { get; set; }

    /// <summary>Geçmiş verilere dayalı bırakma/çekilme oranı</summary>
    public decimal AttritionRate { get; set; }

    /// <summary>Attrition hesaplama yöntemi: "Course", "Department", "System default"</summary>
    public string AttritionMethod { get; set; } = string.Empty;

    /// <summary>Tahmin adımlarının izlenebilirlik kaydı (includeTrace=true ile döner)</summary>
    public ForecastTraceDto? Trace { get; set; }

    // --- Faz 3: Sequential Dependency + Volatility ---

    /// <summary>Ardışık ders bağımlılık bilgileri (önkoşul derslerin mevcut dönem kayıt etkisi)</summary>
    public List<SequentialDependencyDto> SequentialDependencies { get; set; } = new();

    // --- Faz 4: Simulation ---

    /// <summary>Simülasyon sonucu mu (true ise gerçek tahmin değil, override'lı)</summary>
    public bool IsSimulation { get; set; }

    /// <summary>Uygulanan simülasyon parametreleri (IsSimulation=true ise dolu)</summary>
    public ForecastSimulationParams? SimulationParams { get; set; }

    /// <summary>Ders kayıt volatilite skoru (CV — coefficient of variation)</summary>
    public decimal VolatilityScore { get; set; }

    /// <summary>Volatilite seviyesi: Stable, Moderate, Volatile</summary>
    public string VolatilityLevel { get; set; } = string.Empty;
}

public class DemandBreakdownDto
{
    public int RetakeFF { get; set; }
    public int RetakeNA { get; set; }
    public int RetakeU { get; set; }
    public int RegularOnSchedule { get; set; }
    public int BehindSchedule { get; set; }
    public int UpperCourse { get; set; }
    public int EctsLimited { get; set; }

    public int TotalRetake => RetakeFF + RetakeNA + RetakeU;
    public int TotalEligible => RegularOnSchedule + BehindSchedule;
}

public class HistoricalEnrollmentDto
{
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int ActualEnrollment { get; set; }
    public int SectionCount { get; set; }
}

public class ConfidenceRangeDto
{
    public int LowerBound { get; set; }
    public int UpperBound { get; set; }
    public int Margin { get; set; }
    public string Method { get; set; } = string.Empty;       // "Historical StdDev" or "Rule-of-Thumb"
}

public class SectionDistributionDto
{
    public int RecommendedSections { get; set; }
    public int EffectiveCapacityPerSection { get; set; }
    public int TotalCapacity { get; set; }
    public decimal UtilizationRate { get; set; }
    public int SurplusCapacity { get; set; }
    public List<int> StudentsPerSection { get; set; } = new();   // e.g. [30, 29, 29]
    public List<string> Warnings { get; set; } = new();
}

public class CrossDepartmentDemandDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public int EstimatedStudentCount { get; set; }
}

/// <summary>
/// Seçmeli ders havuz dağılım bilgisi.
/// Popülariteye dayalı fraksiyonel talep dağıtımı sonucunu gösterir.
/// </summary>
public class ElectiveDistributionDto
{
    public int ElectiveGroupId { get; set; }
    public string ElectiveGroupName { get; set; } = string.Empty;
    public int TotalCoursesInPool { get; set; }
    public int RequiredSelections { get; set; }
    public decimal PopularityScore { get; set; }
    public int EligibleStudentsInPool { get; set; }
    public decimal DistributedDemandShare { get; set; }
}

/// <summary>
/// Tahmin adımlarının izlenebilirlik kaydı.
/// Her adım giriş→çıkış değerlerini, uygulanan faktörü ve veri kaynağını gösterir.
/// </summary>
public class SequentialDependencyDto
{
    public int PrerequisiteCourseId { get; set; }
    public string PrerequisiteCourseCode { get; set; } = string.Empty;
    public int CurrentPrereqEnrollment { get; set; }
    public int HistoricalPrereqAverage { get; set; }
    public decimal AdjustmentFactor { get; set; }
}

public class ForecastTraceDto
{
    public List<ForecastTraceStepDto> Steps { get; set; } = new();
    public decimal FinalRuleWeight { get; set; }
    public decimal FinalHistoricalWeight { get; set; }
}

public class ForecastTraceStepDto
{
    public string StepName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int InputValue { get; set; }
    public int OutputValue { get; set; }
    public decimal? Factor { get; set; }
    public string? DataSource { get; set; }
}

/// <summary>
/// What-If simülasyon parametreleri.
/// Admin farklı kapasite/section/show rate senaryolarını test edebilir.
/// </summary>
public class ForecastSimulationParams
{
    public int? OverrideCapacityPerSection { get; set; }
    public int? OverrideSectionCount { get; set; }
    public decimal? OverrideShowRate { get; set; }
    public decimal? OverrideAttritionRate { get; set; }
    public int? OverrideExpectedEnrollment { get; set; }
}
