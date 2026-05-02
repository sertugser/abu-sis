namespace SIS.Domain.Entities;

/// <summary>
/// Otomatik ders programlama algoritmasını etkileyen ayarlar (SA, MRV, LNS,
/// kısıt eşikleri, vb.). Genel SystemConfig'den ayrı bir tabloda — domain
/// ayrımı için. Admin yönetimi /ders-programi-kisitlamalari sayfasından.
/// </summary>
public class SchedulingAlgorithmConfig
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;    // SA, MRV, LNS, HardThreshold, SoftThreshold, Other
    public string DataType { get; set; } = "string";        // string, int, decimal, bool, json
    public uint RowVersion { get; set; }
}
