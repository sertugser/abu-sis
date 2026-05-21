namespace SIS.Domain.Identity;

public class PermissionBundleItem
{
    public int BundleId { get; set; }
    public int PermissionId { get; set; }

    public PermissionBundle? Bundle { get; set; }
    public Permission? Permission { get; set; }
}
