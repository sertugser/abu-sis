namespace SIS.Domain.Entities;

public class ConstraintException
{
    public int Id { get; set; }
    public int ConstraintDefinitionId { get; set; }
    public int? InstructorId { get; set; }                      // null = all instructors
    public int? StudentId { get; set; }                         // null = all students
    public int? DepartmentId { get; set; }                      // null = all departments
    public int? CourseSectionId { get; set; }                   // null = all sections
    public int? TermId { get; set; }                            // null = all terms
    public bool IsExempt { get; set; } = true;                  // true = skip this constraint
    public string? ParameterOverride { get; set; }              // JSON: override params for this entity
    public string? Reason { get; set; }                         // Why this exception exists
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ConstraintDefinition ConstraintDefinition { get; set; } = null!;
    public Instructor? Instructor { get; set; }
    public Student? Student { get; set; }
    public Department? Department { get; set; }
    public CourseSection? CourseSection { get; set; }
    public AcademicTerm? Term { get; set; }
}
