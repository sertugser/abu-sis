namespace SIS.Domain.Entities;

/// <summary>
/// Bir duyurunun bir kullanıcı tarafından okunma kaydı. Composite PK: (AnnouncementId, UserId).
/// </summary>
/// <remarks>
/// <para><b>ReadAt</b> — kullanıcı duyuru detayını ilk açtığında ayarlanır (pasif okuma).</para>
/// <para><b>AcknowledgedAt</b> — yalnızca pop-up zorunlu duyurularda kullanıcı açıkça "Okudum, anladım"
/// butonuna bastığında ayarlanır (Madde 6 ile devreye girer). Normal duyurularda null kalır.</para>
/// </remarks>
public class AnnouncementRead
{
    public int AnnouncementId { get; set; }
    public InternalAnnouncement Announcement { get; set; } = null!;

    public int UserId { get; set; }

    public DateTime ReadAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}
