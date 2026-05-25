namespace SIS.Application.Admin.Services;

/// <summary>
/// Öğrenci belgelerinin resmî PDF üreticisi. CourseHistoryPdfService deseniyle
/// hizalı: QuestPDF fluent ağacı, T.C. + üniversite başlığı, üretim zaman damgası.
/// </summary>
public interface IStudentDocumentPdfService
{
    /// <summary>
    /// Öğrenci Bilgi Formu (kimlik + program + iletişim + askerlik blokları, varsa fotoğraf).
    /// Öğrenci yoksa null döner.
    /// </summary>
    Task<byte[]?> GenerateStudentInfoFormAsync(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Öğrenci Belgesi (kısa resmî kayıtlılık metni + imza bloğu).
    /// Öğrenci yoksa null döner.
    /// </summary>
    Task<byte[]?> GenerateEnrollmentCertificateAsync(int studentId, CancellationToken ct = default);
}
