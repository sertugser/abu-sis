namespace SIS.Domain.Entities;

/// <summary>
/// Kritik işlemlerin sorgulanabilir logu.
/// Kim, ne zaman, ne yaptı, eski değer → yeni değer.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValue { get; set; }   // JSON
    public string? NewValue { get; set; }   // JSON
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
}
