namespace SIS.Domain.Entities;

public class Curriculum
{
    public int Id { get; set; }
    public int ProgramId { get; set; }
    public AcademicProgram Program { get; set; } = null!;
    public string Version { get; set; } = string.Empty;    // e.g., "2022"
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<CurriculumCourse> CurriculumCourses { get; set; } = new List<CurriculumCourse>();
}
