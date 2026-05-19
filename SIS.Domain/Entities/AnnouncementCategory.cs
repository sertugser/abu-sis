namespace SIS.Domain.Entities;

/// <summary>
/// Duyuru kategorisi (Akademik, İdari, Sosyal, Ders, vb.).
/// Admin tarafından yönetilir; sistem kategorileri (<see cref="IsSystem"/> = true) silinemez,
/// yalnızca görsel özellikleri düzenlenebilir.
/// </summary>
public class AnnouncementCategory
{
    public int Id { get; set; }

    /// <summary>Kategorinin görüntülenen adı (örn. "Akademik").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>HEX renk kodu (örn. <c>#3b82f6</c>). Badge ve sekme rengi için.</summary>
    public string ColorHex { get; set; } = "#3b82f6";

    /// <summary>Lucide icon adı (örn. "GraduationCap"). Boş ise generic megaphone gösterilir.</summary>
    public string IconName { get; set; } = string.Empty;

    /// <summary>UI'da görüntülenme sırası (küçük değer önce gelir).</summary>
    public int SortOrder { get; set; }

    /// <summary>Sistem tarafından seed edilmiş kategori. Silinemez, ama düzenlenebilir.</summary>
    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
