namespace SIS.Domain.Identity;

/// <summary>
/// Default access matrix: bir kategori bir role açılırsa, o kategorinin tüm raporları
/// o role görünür/erişilebilir olur. Per-rapor istisnalar <see cref="ReportPermissionOverride"/>
/// ile yapılır.
/// </summary>
public class ReportCategoryRolePermission
{
    public int CategoryId { get; set; }
    public int RoleId { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public int? GrantedByUserId { get; set; }

    public ReportCategory? Category { get; set; }
}
