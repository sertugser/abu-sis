using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class ConstraintViolationSnapshot
{
    public int Id { get; set; }
    public int TermId { get; set; }
    public DateTime SnapshotAt { get; set; } = DateTime.UtcNow;
    public int TotalHardViolations { get; set; }
    public double TotalSoftPenalty { get; set; }
    public bool IsFeasible { get; set; }
    public SnapshotType SnapshotType { get; set; }
    public string? CreatedByUserId { get; set; }

    public AcademicTerm Term { get; set; } = null!;
    public ICollection<ConstraintViolationSnapshotDetail> Details { get; set; } = new List<ConstraintViolationSnapshotDetail>();
}
