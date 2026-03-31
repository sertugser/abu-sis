namespace SIS.Domain.Entities;

// Composite PK: (CourseId, RequiredCourseId) — must be configured in OnModelCreating
public class CoursePrerequisite
{
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int RequiredCourseId { get; set; }
    public Course RequiredCourse { get; set; } = null!;
    public bool IsCorequisite { get; set; } // true = aynı dönemde birlikte alınmalı (lecture+lab)
}
