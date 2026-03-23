using SIS.Application.StudentEligibility.DTOs;

namespace SIS.Application.StudentEligibility.Services;

public interface IStudentEligibilityService
{
    /// <summary>
    /// Belirli bir ders icin tum ogrencilerin uygunluk analizini yapar.
    /// </summary>
    Task<CourseEligibilityReportDto?> GetCourseEligibilityReportAsync(
        int courseId,
        int? departmentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirli bir ogrencinin tum mufredattaki derslere uygunlugunu analiz eder.
    /// </summary>
    Task<List<StudentCourseEligibilityDto>> GetStudentEligibleCoursesAsync(
        int studentId,
        CancellationToken cancellationToken = default);
}
