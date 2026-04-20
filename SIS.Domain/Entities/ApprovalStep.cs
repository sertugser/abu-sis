using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class ApprovalStep
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public int StepOrder { get; set; }
    public string RequiredRole { get; set; } = string.Empty;
    public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Pending;
    public string? DecidedByUserId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Comment { get; set; }

    public ConstraintExceptionRequest Request { get; set; } = null!;
}
