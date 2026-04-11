namespace SIS.Domain.Entities;

public class ConstraintDefinition
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;           // "H1", "S2", etc.
    public string Name { get; set; } = string.Empty;           // Human-readable name
    public string? Description { get; set; }                    // What this constraint does
    public string Category { get; set; } = "Hard";             // "Hard" or "Soft"
    public string Scope { get; set; } = "Scheduling";          // "Scheduling" or "Registration"
    public bool IsEnabled { get; set; } = true;
    public string? Parameters { get; set; }                     // JSON: {"MaxConsecutiveHours": 4}
    public int SortOrder { get; set; }                          // Display order
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ConstraintException> Exceptions { get; set; } = new List<ConstraintException>();
}
