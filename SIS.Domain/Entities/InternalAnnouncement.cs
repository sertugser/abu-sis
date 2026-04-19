using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class InternalAnnouncement
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public AnnouncementAudience TargetAudience { get; set; }
    public int? TargetDepartmentId { get; set; }
    public Department? TargetDepartment { get; set; }
    public bool IsPinned { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
