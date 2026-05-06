using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class AcademicCvActivity
{
    public int Id { get; set; }
    public int InstructorId { get; set; }
    public Instructor Instructor { get; set; } = null!;
    public int ActivityYear { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
