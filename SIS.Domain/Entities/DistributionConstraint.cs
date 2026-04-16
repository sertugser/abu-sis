using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class DistributionConstraint
{
    public int Id { get; set; }
    public DistributionType DistributionType { get; set; }
    public PreferenceLevel PreferenceLevel { get; set; }
    public int TermId { get; set; }
    public int? DepartmentId { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public AcademicTerm Term { get; set; } = null!;
    public Department? Department { get; set; }
    public ICollection<DistributionConstraintCourse> Courses { get; set; } = new List<DistributionConstraintCourse>();
}
