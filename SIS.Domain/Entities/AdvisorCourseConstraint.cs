using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class AdvisorCourseConstraint
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public int AdvisorId { get; set; }
    public Instructor Advisor { get; set; } = null!;
    public AdvisorConstraintType ConstraintType { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
