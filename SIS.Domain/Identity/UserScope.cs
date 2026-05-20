namespace SIS.Domain.Identity;

public enum ScopeKind
{
    Faculty = 1,
    // Department = 2,  // Phase ileri
    // Program = 3,     // Phase ileri
}

public class UserScope
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ScopeKind Kind { get; set; }
    public int? FacultyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
