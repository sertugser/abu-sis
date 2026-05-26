namespace SIS.Application.Identity.DTOs;

/// <summary>
/// Bir rolün yetki durumu — manuel atanmış ve paketten gelen permission'lar ayrı listelenir.
/// Admin matrisi: manuel olanlar düzenlenebilir, paketten gelenler kilitli (salt-okunur) gösterilir.
/// </summary>
public class RolePermissionStateDto
{
    /// <summary>Manuel atanmış (SourceBundleId IS NULL) permission ID'leri.</summary>
    public List<int> ManualPermissionIds { get; set; } = new();

    /// <summary>Bir veya daha fazla paketten gelen (SourceBundleId IS NOT NULL) permission ID'leri (distinct).</summary>
    public List<int> BundlePermissionIds { get; set; } = new();
}
