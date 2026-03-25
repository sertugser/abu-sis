namespace SIS.Domain.Entities;

public class SchedulingAuditLog
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public CourseSection Section { get; set; } = null!;
    public string Action { get; set; } = string.Empty; // "Assign", "Unassign", "AutoSchedule"
    public string UserId { get; set; } = string.Empty;
    public int? ClassroomId { get; set; }
    public string? ScheduleJson { get; set; }
    public int? InstructorId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
