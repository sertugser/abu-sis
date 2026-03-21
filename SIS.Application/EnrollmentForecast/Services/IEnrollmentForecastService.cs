using SIS.Application.EnrollmentForecast.DTOs;

namespace SIS.Application.EnrollmentForecast.Services;

public interface IEnrollmentForecastService
{
    Task<CourseDemandForecastDto?> GetCourseDemandForecastAsync(int courseId);
    Task<DepartmentForecastSummaryDto?> GetDepartmentSummaryAsync(int departmentId);
}
