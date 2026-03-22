using SIS.Application.EnrollmentForecast.DTOs;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.AcademicOperations.DTOs;

public class AcademicPlanningSummaryDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public DepartmentForecastSummaryDto? ForecastSummary { get; set; }
    public ScheduleValidationResultDto ScheduleValidation { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}
