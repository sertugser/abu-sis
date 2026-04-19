using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class EvaluationPeriod
{
    public int Id { get; set; }
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public DateTime OpenedAt { get; set; }
    public DateTime ClosesAt { get; set; }
    public EvaluationPeriodStatus Status { get; set; }
    public string? OpenedBy { get; set; }
}
