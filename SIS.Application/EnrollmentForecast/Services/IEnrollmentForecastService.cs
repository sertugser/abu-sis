using SIS.Application.EnrollmentForecast.DTOs;

namespace SIS.Application.EnrollmentForecast.Services;

public interface IEnrollmentForecastService
{
    Task<CourseDemandForecastDto?> GetCourseDemandForecastAsync(int courseId);
    Task<DepartmentForecastSummaryDto?> GetDepartmentSummaryAsync(int departmentId);
    Task<int> CalibrateForTermAsync(int termId, CancellationToken cancellationToken = default);
    Task<CourseDemandForecastDto?> SimulateCourseDemandAsync(int courseId, ForecastSimulationParams parameters);
}
