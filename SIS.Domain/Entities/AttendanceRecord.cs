using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class AttendanceRecord
{
    public int Id { get; set; }
    public int AttendanceSessionId { get; set; }
    public AttendanceSession AttendanceSession { get; set; } = null!;
    public int CourseRegistrationId { get; set; }
    public CourseRegistration CourseRegistration { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public AttendanceStatus Status { get; set; }
    public AttendanceEntrySource Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? QrSubmittedAt { get; set; }
}
