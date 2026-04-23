using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class ProgramQuota
{
    public int Id { get; set; }
    public int ProgramId { get; set; }
    public AcademicProgram Program { get; set; } = null!;
    public int Year { get; set; }
    public QuotaType Type { get; set; }
    public int Quantity { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
