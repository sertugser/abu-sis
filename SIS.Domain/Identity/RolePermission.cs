namespace SIS.Domain.Identity;

public class RolePermission
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public int? SourceBundleId { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public int? GrantedByUserId { get; set; }

    public Permission? Permission { get; set; }
    public PermissionBundle? SourceBundle { get; set; }

    public bool IsManual => SourceBundleId is null;
}
