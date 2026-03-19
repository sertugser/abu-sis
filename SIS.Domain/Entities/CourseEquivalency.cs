namespace SIS.Domain.Entities;

// Composite PK: (CourseId, EquivalentCourseId) — must be configured in OnModelCreating
public class CourseEquivalency
{
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int EquivalentCourseId { get; set; }
    public Course EquivalentCourse { get; set; } = null!;
}
