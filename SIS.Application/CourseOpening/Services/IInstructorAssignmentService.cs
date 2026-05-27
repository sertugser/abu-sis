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

    /// <summary>
    /// Kaynak dönemdeki hoca atamalarını hedef dönemin <b>boş (hocası atanmamış)</b> şubelerine kopyalar.
    /// Ders bazında, şube sırasına göre eşler; mevcut atamalara dokunmaz.
    /// </summary>
    Task<CopyInstructorAssignmentsResultDto> CopyInstructorAssignmentsFromTermAsync(
        CopyInstructorAssignmentsRequest request, CancellationToken cancellationToken = default);
}
