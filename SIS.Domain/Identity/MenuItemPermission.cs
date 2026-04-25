namespace SIS.Domain.Identity;

public class MenuItemPermission
{
    public int MenuItemId { get; set; }
    public int PermissionId { get; set; }

    public MenuItem? MenuItem { get; set; }
    public Permission? Permission { get; set; }
}
