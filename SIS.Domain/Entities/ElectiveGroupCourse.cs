namespace SIS.Domain.Entities;

public class ElectiveGroupCourse
{
    public int ElectiveGroupId { get; set; }
    public ElectiveGroup ElectiveGroup { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public DateTime AddedAt { get; set; }
}
