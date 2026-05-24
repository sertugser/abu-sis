namespace SIS.Application.Common.Services;

/// <summary>
/// Öğrenci profil fotoğrafını dosya sistemine yazar/okur/siler. Her öğrenci için
/// tek fotoğraf saklanır; dosya adı SUNUCU tarafından üretilir (öğrenci kimliği +
/// uzantı), istemciden gelen dosya adı asla bir path'e enterpole edilmez.
/// </summary>
public interface IStudentPhotoStorage
{
    /// <summary>
    /// Fotoğrafı kaydeder ve dosya uzantısını döner ("jpg" / "png").
    /// Desteklenmeyen content-type için <see cref="InvalidOperationException"/> fırlatır.
    /// </summary>
    Task<string> SaveAsync(int studentId, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Fotoğrafı okur. Dosya yoksa null döner.
    /// </summary>
    Task<(Stream Stream, string ContentType)?> GetAsync(int studentId, string extension, CancellationToken ct = default);

    /// <summary>
    /// Fotoğrafı siler (yoksa sessizce geçer).
    /// </summary>
    Task DeleteAsync(int studentId, string extension, CancellationToken ct = default);
}
