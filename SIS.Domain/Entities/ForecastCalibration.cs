namespace SIS.Domain.Entities;

public class ForecastCalibration
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public int PredictedDemand { get; set; }
    public int ActualEnrollment { get; set; }
    public int PredictedSections { get; set; }
    public int ActualSections { get; set; }

    public decimal AbsoluteError { get; set; }
    public decimal PercentageError { get; set; }
    public decimal RuleBasedWeight { get; set; }
    public decimal HistoricalWeight { get; set; }

    public int EligibleStudentCount { get; set; }
    public decimal ShowRate { get; set; }

    public DateTime CalibratedAt { get; set; }
}
