namespace SIS.Domain.Entities;

public class CurriculumCourse
{
    public int CurriculumId { get; set; }
    public Curriculum Curriculum { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public bool IsCompulsory { get; set; }
    public int SuggestedSemester { get; set; }             // which semester this course is typically taken
}
