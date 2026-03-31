namespace SIS.Domain.Entities;

/// <summary>
/// Konfigüre edilebilir iş kuralları. Admin panelinden değiştirilebilir.
/// Key-value yapısında. Servis entegrasyonu Faz 4'te yapılacak.
/// </summary>
public class SystemConfig
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;    // e.g., "Registration", "GPA", "Scheduling"
    public string DataType { get; set; } = "string";        // string, int, decimal, bool, json
}
