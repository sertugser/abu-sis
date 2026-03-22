namespace SIS.Domain.Entities;

public class Instructor
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string Title { get; set; } = string.Empty;           // e.g., "Prof.", "Dr."
    public int MaxWeeklyHours { get; set; }                     // rank-based workload cap
    public ICollection<CourseSection> Sections { get; set; } = new List<CourseSection>();
    public ICollection<InstructorAvailability> Availabilities { get; set; } = new List<InstructorAvailability>();
}
