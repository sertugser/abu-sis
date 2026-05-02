namespace SIS.Domain.Entities;

/// <summary>
/// Algoritma parametreleri için grup metadatası — UI'da başlık + renk + sıralama
/// Ders programı kısıtları sayfasındaki "Algoritma Parametreleri" sekmesi bu tabloyu okuyup
/// her gruba sade Türkçe başlık (örn. "Ana Optimizasyon Motoru") + pastel badge rengi atar.
///
/// Bir SchedulingAlgorithmConfig.Category değeri buradaki Category değerine match eder
/// (örn. "SA" → "Ana Optimizasyon Motoru").
/// </summary>
public class SchedulingAlgorithmConfigGroup
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;       // "SA", "MRV", "LNS", "Other", "HardThreshold"
    public string DisplayName { get; set; } = string.Empty;    // "Ana Optimizasyon Motoru"
    public string? Description { get; set; }
    public string? BadgeClass { get; set; }                     // "bg-blue-100 text-blue-800 border-blue-200"
    public int SortOrder { get; set; }
}
