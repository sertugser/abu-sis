namespace SIS.Domain.Entities;

public class ConstraintProfileItem
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public int ConstraintDefinitionId { get; set; }
    public bool IsEnabled { get; set; }
    public string Category { get; set; } = "Hard";
    public int Weight { get; set; } = 5;
    public string? Parameters { get; set; }

    public ConstraintProfile Profile { get; set; } = null!;
    public ConstraintDefinition ConstraintDefinition { get; set; } = null!;
}
