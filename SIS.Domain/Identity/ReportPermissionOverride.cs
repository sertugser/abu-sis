namespace SIS.Domain.Identity;

/// <summary>
/// Per-rapor × rol istisnası. Kategori-rol default'una üstün gelir.
/// IsAllowed = true → kategori erişimi olmasa bile bu raporu aç (allow)
/// IsAllowed = false → kategori erişimi olsa bile bu raporu kapat (deny)
/// Deny, allow'dan baskındır.
/// </summary>
public class ReportPermissionOverride
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public int RoleId { get; set; }
    public bool IsAllowed { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }

    public ReportDefinition? Report { get; set; }
}
