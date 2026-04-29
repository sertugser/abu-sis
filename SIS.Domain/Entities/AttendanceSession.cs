using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class AttendanceSession
{
    public int Id { get; set; }
    public int CourseSectionId { get; set; }
    public CourseSection CourseSection { get; set; } = null!;
    public int InstructorId { get; set; }
    public Instructor Instructor { get; set; } = null!;
    public int WeekNumber { get; set; }
    public DateTime SessionDate { get; set; }
    public string? Topic { get; set; }
    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsQrOpen { get; set; }
    public DateTime? QrExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<AttendanceRecord> Records { get; set; } = new();
}
