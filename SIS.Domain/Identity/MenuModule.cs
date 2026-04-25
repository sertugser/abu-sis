namespace SIS.Domain.Identity;

public class MenuModule
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public MenuModule? Parent { get; set; }
    public ICollection<MenuModule> Children { get; set; } = new List<MenuModule>();
    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}
