using SIS.Application.CourseOpening.DTOs;

namespace SIS.Application.CourseOpening.Services;

/// <summary>
/// Geçmiş dönemden ders aktarımı (Ders Aç (Hareket) sayfası).
/// Section açma akışını <see cref="ICourseSectionLifecycleService"/>'e delege eder.
/// Faz 3.C: Eski ICourseOpeningService'ten ayrıldı, ICourseTransferService'ten yeniden adlandırıldı.
/// </summary>
public interface ITermCourseTransferService
{
    Task<List<PreviousTermCourseDto>> GetPreviousTermCoursesAsync(
        int departmentId, int sourceTermId, int targetTermId,
        int? programId = null, int? classLevel = null, CancellationToken cancellationToken = default);

    Task<TransferFromTermResultDto> TransferFromTermAsync(
        TransferFromTermRequest request, CancellationToken cancellationToken = default);
}
