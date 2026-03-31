using SIS.Domain.Interfaces;

namespace SIS.Domain.Entities;

public class Faculty : IHasAuditFields
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<Department> Departments { get; set; } = new List<Department>();
}
