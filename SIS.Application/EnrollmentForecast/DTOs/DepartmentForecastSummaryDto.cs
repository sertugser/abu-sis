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
}
