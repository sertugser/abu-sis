using SIS.Application.CourseOpening.DTOs;

namespace SIS.Application.CourseOpening.Services;

/// <summary>
/// Toplu öğretim elemanı atama işlemleri.
/// Faz 3.C: Eski ICourseOpeningService'ten ayrıldı.
/// </summary>
public interface IInstructorAssignmentService
{
    Task<BulkAssignInstructorResultDto> BulkAssignInstructorAsync(
        int departmentId, int termId, BulkAssignInstructorRequest request, CancellationToken cancellationToken = default);
}
