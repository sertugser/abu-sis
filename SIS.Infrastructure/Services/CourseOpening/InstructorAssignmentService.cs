using SIS.Application.CourseOpening.DTOs;
using SIS.Application.CourseOpening.Services;

namespace SIS.Infrastructure.Services.CourseOpening;

/// <summary>
/// Toplu öğretim elemanı atama. UpdateSectionAsync'i her section için arka arkaya çağırır.
/// Faz 3.C: Eski CourseOpeningService'ten ayrıldı.
/// </summary>
public class InstructorAssignmentService(ICourseSectionLifecycleService lifecycleService) : IInstructorAssignmentService
{
    private readonly ICourseSectionLifecycleService _lifecycleService = lifecycleService;

    public async Task<BulkAssignInstructorResultDto> BulkAssignInstructorAsync(
        int departmentId, int termId, BulkAssignInstructorRequest request, CancellationToken ct = default)
    {
        var result = new BulkAssignInstructorResultDto();

        foreach (var assignment in request.Assignments)
        {
            try
            {
                await _lifecycleService.UpdateSectionAsync(new UpdateOpenedSectionRequest
                {
                    SectionId = assignment.SectionId,
                    InstructorId = assignment.InstructorId,
                }, ct);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                result.Errors.Add($"Şube #{assignment.SectionId}: {ex.Message}");
            }
        }

        return result;
    }
}
