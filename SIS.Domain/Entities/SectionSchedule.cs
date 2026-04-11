namespace SIS.Domain.Entities;

/// <summary>
/// Bir şubenin hangi TimeSlot'ta hangi derslikte yapıldığını belirler.
/// CourseSection ↔ TimeSlot ↔ Classroom ilişkisi.
/// Authoritative schedule storage (replaced ScheduleJson).
/// </summary>
public class SectionSchedule
{
    public int Id { get; set; }
    public int CourseSectionId { get; set; }
    public CourseSection CourseSection { get; set; } = null!;
    public int TimeSlotId { get; set; }
    public TimeSlot TimeSlot { get; set; } = null!;
    public int ClassroomId { get; set; }
    public Classroom Classroom { get; set; } = null!;
}
