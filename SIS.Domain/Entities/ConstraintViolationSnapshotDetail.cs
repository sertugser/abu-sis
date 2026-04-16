namespace SIS.Domain.Entities;

public class ConstraintViolationSnapshotDetail
{
    public int Id { get; set; }
    public int SnapshotId { get; set; }
    public string ConstraintCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ViolationCount { get; set; }
    public double Penalty { get; set; }
    public double WeightedPenalty { get; set; }
    public int AffectedSectionCount { get; set; }

    public ConstraintViolationSnapshot Snapshot { get; set; } = null!;
}
