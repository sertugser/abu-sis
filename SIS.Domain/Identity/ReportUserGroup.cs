namespace SIS.Domain.Identity;

/// <summary>Adlandırılmış kullanıcı kümesi (örn. "Fakülte Sekreterleri"). Rapor yetkisi gruba verilebilir.</summary>
public class ReportUserGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ReportUserGroupMember> Members { get; set; } = new();
}
