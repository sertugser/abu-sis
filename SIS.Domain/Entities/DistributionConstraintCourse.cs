namespace SIS.Domain.Entities;

public class DistributionConstraintCourse
{
    public int Id { get; set; }
    public int DistributionConstraintId { get; set; }
    public int CourseId { get; set; }
    public int? CourseSectionId { get; set; }
    public int SortOrder { get; set; }

    public DistributionConstraint DistributionConstraint { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public CourseSection? CourseSection { get; set; }
}
