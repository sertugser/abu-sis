namespace SIS.Domain.Identity;

public class MenuItem
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool OpenInNewTab { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public MenuModule? Module { get; set; }
    public ICollection<MenuItemPermission> MenuItemPermissions { get; set; } = new List<MenuItemPermission>();
}
