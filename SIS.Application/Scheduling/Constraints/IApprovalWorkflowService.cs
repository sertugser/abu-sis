using SIS.Domain.Entities;

namespace SIS.Application.Scheduling.Constraints;

public interface IApprovalWorkflowService
{
    Task<ConstraintExceptionRequest> CreateRequestAsync(ConstraintExceptionRequest request, CancellationToken ct);
    Task<(bool Success, string? Error)> ApproveStepAsync(int requestId, string userId, string? comment, CancellationToken ct);
    Task<(bool Success, string? Error)> RejectStepAsync(int requestId, string userId, string? comment, CancellationToken ct);
}
