using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

/// <summary>
/// Kişiye özel, sistem üretimi bildirim. Duyurudan (Announcement) farkı: tek kullanıcıya
/// yöneliktir, bir olaydan tetiklenir ve genelde bir aksiyona/sayfaya götürür (Link).
/// Okundu durumu burada tutulur (duyurulardaki AnnouncementRead deseninden bağımsız).
/// </summary>
public class Notification
{
    public int Id { get; set; }

    /// <summary>Hedef kullanıcı (ApplicationUser.Id — int).</summary>
    public int UserId { get; set; }

    public NotificationType Type { get; set; }
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>Tıklanınca gidilecek frontend route'u (ör. "/ders-kayit-onay?studentId=42"). Opsiyonel.</summary>
    public string? Link { get; set; }

    /// <summary>İlgili kaydın referansı (ör. "registration:42"). Gruplama/dedup için opsiyonel.</summary>
    public string? EntityRef { get; set; }

    /// <summary>Aynı türden çoklu olayı tek satırda toplamak için anahtar (Faz 3). Opsiyonel.</summary>
    public string? GroupKey { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Bu tarihten sonra gösterilmez/temizlenebilir. Opsiyonel.</summary>
    public DateTime? ExpiresAt { get; set; }
}
