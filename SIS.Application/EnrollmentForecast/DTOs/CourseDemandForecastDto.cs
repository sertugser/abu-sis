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
