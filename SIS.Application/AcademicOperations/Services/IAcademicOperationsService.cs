using SIS.Application.AcademicOperations.DTOs;

namespace SIS.Application.AcademicOperations.Services;

public interface IAcademicOperationsService
{
    Task<AcademicOperationsContextDto> GetContextAsync(int userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
    Task<AcademicPlanningSummaryDto?> GetPlanningSummaryAsync(int departmentId, int? termId, int userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
    Task<DepartmentClassListDto?> GetDepartmentClassListAsync(int departmentId, int? termId, int userId, IReadOnlyCollection<string> roles, string? search = null, CancellationToken cancellationToken = default);
    Task<DepartmentCurriculumOverviewDto?> GetCurriculumOverviewAsync(int departmentId, int userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
    Task<DepartmentStudentListDto?> GetDepartmentStudentsAsync(int departmentId, int userId, IReadOnlyCollection<string> roles, string? search = null, string? status = null, int? programId = null, int? curriculumId = null, int? advisorId = null, CancellationToken cancellationToken = default);
    Task<List<DepartmentCurriculumOptionDto>?> GetDepartmentCurriculumOptionsAsync(int departmentId, int userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
    Task<BulkAcademicOperationResultDto?> BulkAssignAdvisorsAsync(BulkAdvisorAssignmentRequestDto request, int userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
    Task<BulkAcademicOperationResultDto?> BulkAssignCurriculumAsync(BulkCurriculumAssignmentRequestDto request, int userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
}
