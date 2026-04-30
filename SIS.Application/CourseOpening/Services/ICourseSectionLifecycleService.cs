using SIS.Application.CourseOpening.DTOs;

namespace SIS.Application.CourseOpening.Services;

/// <summary>
/// Section yaşam döngüsü: aç / toplu aç / güncelle / kapat / yeniden aç.
/// Faz 3.C: Eski ICourseOpeningService'ten ayrıldı.
/// </summary>
public interface ICourseSectionLifecycleService
{
    Task<List<OpenedCourseSectionDto>> OpenCourseAsync(int departmentId, int termId, OpenCourseRequest request, CancellationToken cancellationToken = default);
    Task<BulkOpenCoursesResultDto> BulkOpenCoursesAsync(int departmentId, int termId, BulkOpenCoursesRequest request, CancellationToken cancellationToken = default);
    Task<OpenedCourseSectionDto> UpdateSectionAsync(UpdateOpenedSectionRequest request, CancellationToken cancellationToken = default);
    Task CloseSectionAsync(int sectionId, CancellationToken cancellationToken = default);
    Task<CloseAllSectionsResultDto> CloseAllSectionsAsync(int courseId, int termId, CancellationToken cancellationToken = default);
    Task ReactivateSectionAsync(int sectionId, CancellationToken cancellationToken = default);
}
