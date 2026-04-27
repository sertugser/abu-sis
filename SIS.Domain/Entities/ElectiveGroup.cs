namespace SIS.Domain.Entities;

using SIS.Domain.Enums;

public class ElectiveGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ElectiveSubtype ElectiveSubtype { get; set; }
    // Faz B B3a: yeni DB-driven FK. B3b consuming kodu güncelleyecek; B3c eski enum'u kaldıracak.
    public int? ElectiveSubtypeId { get; set; }
    public int? FacultyId { get; set; }
    public Faculty? Faculty { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<ElectiveGroupCourse> Courses { get; set; } = new List<ElectiveGroupCourse>();
    public ICollection<CurriculumElectivePool> CurriculumPools { get; set; } = new List<CurriculumElectivePool>();
}
