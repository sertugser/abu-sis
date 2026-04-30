namespace SIS.Domain.Entities;

/// <summary>
/// Web arayüzündeki modül/panel davranışlarını yöneten yerel ayarlar.
/// Sistem geneli iş kuralları için SystemConfig kullanılmaya devam eder.
/// </summary>
public class WebModuleSetting
{
    public int Id { get; set; }
    public string ModuleKey { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public uint RowVersion { get; set; }
}
