using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IAdminCourseCatalogService
{
    Task<List<CourseListItemDto>> GetCoursesAsync(CourseFilterDto filter);
    Task<CourseDetailDto?> GetCourseByIdAsync(int id);
    Task<CourseDetailDto> CreateCourseAsync(CreateCourseDto dto);
    Task<CourseDetailDto> UpdateCourseAsync(int id, UpdateCourseDto dto);
    Task DeleteCourseAsync(int id);

    Task AddPrerequisiteAsync(int courseId, AddPrerequisiteDto dto);
    Task RemovePrerequisiteAsync(int courseId, int requiredCourseId);

    Task AddEquivalencyAsync(int courseId, AddEquivalencyDto dto);
    Task RemoveEquivalencyAsync(int courseId, int equivalentCourseId);

    // Proliz "Diğer Ön Koşul Tanımları" — sadece course-level kural alanlarını günceller.
    Task<CourseDetailDto> UpdateCoursePrereqRulesAsync(int courseId, UpdateCoursePrereqRulesDto dto);
}
