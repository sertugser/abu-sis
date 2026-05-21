namespace SIS.Domain.Identity;

/// <summary>
/// Tekil rapor tanımı (Code = stable slug, rapor altyapısı geldiğinde de kalır).
/// Şu an placeholder: rapor altyapısı (template, query engine vs) sonra şekillenince
/// bu tablo zenginleştirilebilir; Code alanı sözleşmeli.
/// </summary>
public class ReportDefinition
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // unique slug
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ReportCategory? Category { get; set; }
}
