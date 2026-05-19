namespace SIS.Domain.Entities;

/// <summary>
/// Bir duyuruya iliştirilmiş dosya eki (PDF, Word, Excel, resim, vb.).
/// Fiziksel dosya <see cref="StorageFileName"/> ile diskte saklanır;
/// <see cref="FileName"/> kullanıcıya gösterilen ve indirilirken kullanılan orijinal addır.
/// </summary>
public class AnnouncementAttachment
{
    public int Id { get; set; }

    public int AnnouncementId { get; set; }
    public InternalAnnouncement Announcement { get; set; } = null!;

    /// <summary>Kullanıcının yüklediği orijinal dosya adı (gösterim ve indirme için).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Disk üzerindeki gerçek dosya adı — çakışma önlemek için GUID.uzantı formatında.</summary>
    public string StorageFileName { get; set; } = string.Empty;

    /// <summary>MIME tipi (örn. <c>application/pdf</c>). Whitelist sanitize edilmiş.</summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>Dosya boyutu byte cinsinden.</summary>
    public long SizeBytes { get; set; }

    public DateTime UploadedAt { get; set; }

    /// <summary>Yükleyenin UserId'si (string, Identity claim ile uyumlu).</summary>
    public string UploadedBy { get; set; } = string.Empty;
}
