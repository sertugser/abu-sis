namespace SIS.Application.Identity.DTOs;

public class UpdateRolePermissionsDto
{
    /// <summary>
    /// Bu rolün sahip olacağı tüm permission ID'leri (replace-all semantiği).
    /// Boş liste = role hiç permission yok.
    /// </summary>
    public List<int> PermissionIds { get; set; } = new();
}
