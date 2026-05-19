using SIS.Application.Announcement.DTOs;

namespace SIS.Application.Announcement.Services;

public interface IAnnouncementAttachmentService
{
    /// <summary>
    /// Bir duyuruya yeni dosya ekler. Settings'teki limitleri (boyut, sayı) ve
    /// politikadaki uzantı/MIME whitelist'ini uygular.
    /// </summary>
    Task<AnnouncementAttachmentDto> UploadAsync(
        int announcementId,
        string originalFileName,
        Stream content,
        long sizeBytes,
        string? clientMimeType,
        string uploadedBy,
        CancellationToken ct = default);

    /// <summary>İndirme için ek metadata + stream döner. Yoksa null.</summary>
    Task<AttachmentDownloadDto?> OpenForDownloadAsync(int attachmentId, CancellationToken ct = default);

    /// <summary>
    /// Eki siler. Sahibi olmadıkça ya da admin override yoksa
    /// <see cref="UnauthorizedAccessException"/> fırlatır.
    /// </summary>
    Task<bool> DeleteAsync(int attachmentId, string requestingUserId, bool isAdminOverride, CancellationToken ct = default);

    /// <summary>
    /// Duyuru kalıcı silinirken ona bağlı tüm ekleri (DB satırı + fiziksel dosya) temizler.
    /// </summary>
    Task DeleteAllForAnnouncementAsync(int announcementId, CancellationToken ct = default);

    /// <summary>Belirtilen duyurunun parent ID'sini döner — visibility kontrolü için.</summary>
    Task<int?> GetParentAnnouncementIdAsync(int attachmentId, CancellationToken ct = default);
}

public sealed record AttachmentDownloadDto(
    Stream Content,
    long SizeBytes,
    string FileName,
    string MimeType);
