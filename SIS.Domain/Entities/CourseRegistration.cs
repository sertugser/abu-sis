using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class CourseRegistration
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int CourseSectionId { get; set; }
    public CourseSection CourseSection { get; set; } = null!;
    // Term is derived via CourseSection.TermId - no direct FK needed
    public RegistrationStatus Status { get; set; }
    public GradeCode? Grade { get; set; }
    public bool IsRepeated { get; set; }                       // FR-56C: grade improvement repeat
    public bool MandatoryAttendance { get; set; }              // FR-106
    public int CourseWithdrawalCount { get; set; }             // how many times this specific course was withdrawn
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
