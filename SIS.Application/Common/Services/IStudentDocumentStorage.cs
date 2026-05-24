namespace SIS.Application.Common.Services;

/// <summary>
/// Öğrenci belgelerini (PDF/JPEG/PNG) dosya sistemine yazar/okur/siler. Dosya adı
/// SUNUCU tarafından üretilir (<c>{studentId}_{documentId}.{ext}</c>), istemciden
/// gelen dosya adı asla bir path'e enterpole edilmez (path traversal engellenir).
/// </summary>
public interface IStudentDocumentStorage
{
    /// <summary>
    /// Belgeyi kaydeder ve dosya uzantısını döner ("pdf" / "jpg" / "png").
    /// Desteklenmeyen content-type için <see cref="InvalidOperationException"/> fırlatır.
    /// </summary>
    Task<string> SaveAsync(int studentId, int documentId, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Belgeyi okur. Dosya yoksa (veya stored uzantı whitelist dışıysa) null döner.
    /// </summary>
    Task<(Stream Stream, string ContentType)?> GetAsync(int studentId, int documentId, string extension, CancellationToken ct = default);

    /// <summary>
    /// Belgeyi siler (yoksa veya stored uzantı whitelist dışıysa sessizce geçer).
    /// </summary>
    Task DeleteAsync(int studentId, int documentId, string extension, CancellationToken ct = default);
}
