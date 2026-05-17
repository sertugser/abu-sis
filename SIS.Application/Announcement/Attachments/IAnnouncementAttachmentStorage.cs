namespace SIS.Application.Announcement.Attachments;

/// <summary>
/// Duyuru eklerinin fiziksel depolanmasını soyutlar. Default implementasyon yerel
/// dosya sistemini kullanır; ileride S3 / Azure Blob için ek implementasyon eklenebilir.
/// </summary>
public interface IAnnouncementAttachmentStorage
{
    /// <summary>
    /// Verilen stream'i depolamaya yazar. Çakışma önlemek için GUID tabanlı dosya adı üretilir.
    /// </summary>
    /// <param name="originalFileName">Kullanıcının verdiği orijinal ad (sadece uzantı için kullanılır)</param>
    /// <param name="content">Yüklenen dosyanın içeriği (stream)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Depolamadaki dosya adı (DB'ye <c>StorageFileName</c> olarak kaydedilir)</returns>
    Task<string> StoreAsync(string originalFileName, Stream content, CancellationToken ct = default);

    /// <summary>
    /// Depolamadan dosyayı okumak için stream döner. Caller stream'i dispose etmelidir.
    /// </summary>
    /// <returns>Stream + boyut + mime hint; dosya yoksa <c>null</c></returns>
    Task<AttachmentReadResult?> OpenReadAsync(string storageFileName, CancellationToken ct = default);

    /// <summary>Dosyayı depolamadan siler. Dosya zaten yoksa hata fırlatmaz.</summary>
    Task DeleteAsync(string storageFileName, CancellationToken ct = default);
}

public sealed record AttachmentReadResult(Stream Content, long SizeBytes);
