using SIS.Application.AcademicOperations.DTOs;

namespace SIS.Application.AcademicOperations.Services;

public interface IAcademicOperationsService
{
    Task<AcademicOperationsContextDto> GetContextAsync(int userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
    Task<AcademicPlanningSummaryDto?> GetPlanningSummaryAsync(int departmentId, int? termId, int userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
    Task<DepartmentClassListDto?> GetDepartmentClassListAsync(int departmentId, int? termId, int userId, IReadOnlyCollection<string> roles, string? search = null, CancellationToken cancellationToken = default);
    Task<DepartmentCurriculumOverviewDto?> GetCurriculumOverviewAsync(int departmentId, int userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    // Section approval
    Task<List<CourseSectionApprovalDto>> GetApprovalsAsync(int departmentId, int termId, CancellationToken cancellationToken = default);
    Task<CourseSectionApprovalDto> ApproveCourseSectionsAsync(int departmentId, int termId, ApproveCourseSectionsRequest request, string userId, CancellationToken cancellationToken = default);
    Task<CourseSectionApprovalDto> RejectCourseSectionsAsync(int departmentId, int termId, RejectCourseSectionsRequest request, string userId, CancellationToken cancellationToken = default);
    Task<BulkApprovalResultDto> BulkApproveAsync(int departmentId, int termId, BulkApprovalRequest request, string userId, CancellationToken cancellationToken = default);
}
