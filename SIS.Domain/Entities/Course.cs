using SIS.Domain.Interfaces;

namespace SIS.Domain.Entities;

public class Course : IHasAuditFields
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }        // owning department (nullable: some courses are cross-departmental)
    public Department? Department { get; set; }
    public int Ects { get; set; }
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public bool IsNonGpa { get; set; }                      // true = S/U graded
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<CoursePrerequisite> Prerequisites { get; set; } = new List<CoursePrerequisite>();
    public ICollection<CourseEquivalency> Equivalencies { get; set; } = new List<CourseEquivalency>();
}
