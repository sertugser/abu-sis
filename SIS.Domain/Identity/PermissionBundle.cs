namespace SIS.Domain.Identity;

public class PermissionBundle
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsSystemDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PermissionBundleItem> Items { get; set; } = new List<PermissionBundleItem>();
    public ICollection<RolePermission> AppliedRolePermissions { get; set; } = new List<RolePermission>();
}
