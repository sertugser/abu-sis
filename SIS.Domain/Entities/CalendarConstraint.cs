using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class CalendarConstraint
{
    public int Id { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public CalendarConstraintType ConstraintType { get; set; }
    public CalendarEventType? TargetEventType { get; set; }
    public string? TargetChainCode { get; set; }
    public string ParameterJson { get; set; } = "{}";
    public bool IsHard { get; set; } = true;
    public int Priority { get; set; } = 100;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
