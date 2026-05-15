namespace SIS.Domain.Identity;

/// <summary>
/// Rapor kategorisi (örn. "academic" / "Akademik Raporlar").
/// Yetkilendirmenin temel granülaritesi: kategoriye access verilince o kategorinin
/// tüm raporları otomatik açılır. Per-rapor override <see cref="ReportPermissionOverride"/>
/// ile yapılır.
/// </summary>
public class ReportCategory
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ReportDefinition> Reports { get; set; } = new();
}
