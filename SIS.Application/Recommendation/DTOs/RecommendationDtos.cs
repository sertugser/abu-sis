using SIS.Domain.Enums;

namespace SIS.Application.Recommendation.DTOs;

public class StudentRecommendationsDto
{
    public int StudentId { get; set; }
    public int PacePreference { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public List<CourseRecommendationDto> Recommendations { get; set; } = new();
}

public class CourseRecommendationDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Ects { get; set; }
    public bool IsCompulsory { get; set; }
    public int SuggestedSemester { get; set; }
    public string TakeType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal PriorityScore { get; set; }
    public CourseDifficultyDto Difficulty { get; set; } = new();
    public CriticalPathAnalysisDto CriticalPath { get; set; } = new();
    public GradeCode? PreviousGrade { get; set; }
    public AdvisorConstraintType? AdvisorConstraintType { get; set; }
    public string? AdvisorConstraintNote { get; set; }
    public List<string> Reasons { get; set; } = new();
}

public class CourseDifficultyDto
{
    public int CourseId { get; set; }
    public decimal Score { get; set; }
    public decimal FailureRateComponent { get; set; }
    public decimal GradeDistributionComponent { get; set; }
    public decimal RetakeDifficultyComponent { get; set; }
    public decimal EctsComponent { get; set; }
    public decimal TrendComponent { get; set; }
}

public class CriticalPathAnalysisDto
{
    public int CourseId { get; set; }
    public int EstimatedDelaySemesters { get; set; }
    public List<string> DependentCourses { get; set; } = new();
}
