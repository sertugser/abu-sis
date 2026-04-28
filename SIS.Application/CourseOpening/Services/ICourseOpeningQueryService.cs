using SIS.Application.CourseOpening.DTOs;

namespace SIS.Application.CourseOpening.Services;

/// <summary>
/// Ders açma akışı için liste/lookup sorguları (read-only).
/// Faz 3.C: Eski ICourseOpeningService'ten ayrıldı.
/// </summary>
public interface ICourseOpeningQueryService
{
    Task<OpenedCourseListDto> GetOpenedCoursesAsync(int departmentId, int termId, CancellationToken cancellationToken = default);
    Task<List<CurriculumCourseForOpeningDto>> GetCurriculumCoursesForOpeningAsync(int departmentId, int termId, bool showAll = false, CancellationToken cancellationToken = default);
    Task<List<InstructorOptionDto>> GetDepartmentInstructorsAsync(int departmentId, int? termId = null, CancellationToken cancellationToken = default);
    Task<List<ExternalCourseDto>> GetExternalCoursesAsync(int departmentId, int termId, CancellationToken cancellationToken = default);
}
