using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class ConstraintExceptionRequest
{
    public int Id { get; set; }
    public int ConstraintDefinitionId { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public ExceptionRequestStatus Status { get; set; } = ExceptionRequestStatus.Pending;

    // Exception details (copied to ConstraintException on final approval)
    public int? InstructorId { get; set; }
    public int? StudentId { get; set; }
    public int? DepartmentId { get; set; }
    public int? CourseSectionId { get; set; }
    public int? TermId { get; set; }
    public bool IsExempt { get; set; } = true;
    public string? ParameterOverride { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }

    public ConstraintDefinition ConstraintDefinition { get; set; } = null!;
    public ICollection<ApprovalStep> ApprovalSteps { get; set; } = new List<ApprovalStep>();
}
