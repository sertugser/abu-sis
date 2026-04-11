namespace SIS.Domain.Entities;

public class CurriculumElectivePool
{
    public int Id { get; set; }
    public int CurriculumId { get; set; }
    public Curriculum Curriculum { get; set; } = null!;
    public int ElectiveGroupId { get; set; }
    public ElectiveGroup ElectiveGroup { get; set; } = null!;
    public int SuggestedSemesterStart { get; set; }
    public int SuggestedSemesterEnd { get; set; }
    public int RequiredCourseCount { get; set; }
    public int RequiredEcts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
