using SIS.Domain.Interfaces;

namespace SIS.Domain.Entities;

public class Department : IHasAuditFields
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public Faculty Faculty { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<AcademicProgram> Programs { get; set; } = new List<AcademicProgram>();
}
