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
}
