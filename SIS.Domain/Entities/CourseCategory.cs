namespace SIS.Domain.Entities;

/// <summary>
/// Lookup entity for "Ders Kategorisi" (Course Category).
/// DB-driven (admin-manageable in future).
/// </summary>
public class CourseCategory
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
