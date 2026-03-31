using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class AcademicProgram
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public DegreeLevel DegreeLevel { get; set; }
    public EducationType EducationType { get; set; }
    public int MaxStudyDurationSemesters { get; set; }      // e.g., 14 for 7-year max
    public int TotalRequiredEcts { get; set; }              // 240 for bachelor, 120 for associate
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<Curriculum> Curricula { get; set; } = new List<Curriculum>();
}
