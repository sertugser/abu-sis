using SIS.Domain.Enums;

namespace SIS.Domain.Identity;

public class Permission
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public PermissionCategory Category { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<MenuItemPermission> MenuItemPermissions { get; set; } = new List<MenuItemPermission>();
    public ICollection<PermissionBundleItem> BundleItems { get; set; } = new List<PermissionBundleItem>();
}
