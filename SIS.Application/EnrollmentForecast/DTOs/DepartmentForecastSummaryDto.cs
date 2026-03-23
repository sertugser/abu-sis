namespace SIS.Application.EnrollmentForecast.DTOs;

public class DepartmentForecastSummaryDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int TotalCoursesAnalyzed { get; set; }
    public int TotalPredictedDemand { get; set; }
    public int TotalRecommendedSections { get; set; }
    public List<CourseDemandForecastDto> CourseSummaries { get; set; } = new();
    public List<string> Notes { get; set; } = new();

    // --- Enhanced fields ---
    public DepartmentCapacitySummaryDto CapacitySummary { get; set; } = new();
}

public class DepartmentCapacitySummaryDto
{
    public int TotalCurrentSections { get; set; }
    public int TotalRecommendedSections { get; set; }
    public int SectionDelta { get; set; }                // recommended - current (positive = need more)
    public int CoursesNeedingMoreSections { get; set; }
    public int CoursesWithExcessCapacity { get; set; }
}
