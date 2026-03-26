using SIS.Application.CourseOpening.DTOs;

namespace SIS.Application.CourseOpening.Services;

public interface ICourseOpeningService
{
    Task<OpenedCourseListDto> GetOpenedCoursesAsync(int departmentId, int termId, CancellationToken cancellationToken = default);
    Task<List<CurriculumCourseForOpeningDto>> GetCurriculumCoursesForOpeningAsync(int departmentId, int termId, CancellationToken cancellationToken = default);
    Task<List<InstructorOptionDto>> GetDepartmentInstructorsAsync(int departmentId, CancellationToken cancellationToken = default);
    Task<List<OpenedCourseSectionDto>> OpenCourseAsync(int departmentId, int termId, OpenCourseRequest request, CancellationToken cancellationToken = default);
    Task<BulkOpenCoursesResultDto> BulkOpenCoursesAsync(int departmentId, int termId, BulkOpenCoursesRequest request, CancellationToken cancellationToken = default);
    Task<OpenedCourseSectionDto> UpdateSectionAsync(UpdateOpenedSectionRequest request, CancellationToken cancellationToken = default);
    Task CloseSectionAsync(int sectionId, CancellationToken cancellationToken = default);
}
