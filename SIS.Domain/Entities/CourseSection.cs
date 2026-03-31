namespace SIS.Domain.Entities;

public class CourseSection
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public int? InstructorId { get; set; }
    public Instructor? Instructor { get; set; }
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public int? ClassroomId { get; set; }
    public Classroom? Classroom { get; set; }
    public int SectionNumber { get; set; }
    public bool IsActive { get; set; }
    public bool IsFinalized { get; set; }
    public uint RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    // Schedule stored as JSON: list of {DayOfWeek, StartTime, EndTime}
    public string ScheduleJson { get; set; } = "[]";
    public ICollection<CourseRegistration> Registrations { get; set; } = new List<CourseRegistration>();
}
