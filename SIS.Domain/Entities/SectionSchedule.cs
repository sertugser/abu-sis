using SIS.Domain.Enums;

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

    /// <summary>
    /// Bu slot teorik mi uygulama mı? Default: Theory.
    /// Scheduler, dersin Course.TheoryHours/LabHours değerlerine göre doldurur.
    /// Devamsızlık eşikleri ve e-yoklama entegrasyonu için kullanılır.
    /// </summary>
    public ScheduleBlockType BlockType { get; set; } = ScheduleBlockType.Theory;
}
