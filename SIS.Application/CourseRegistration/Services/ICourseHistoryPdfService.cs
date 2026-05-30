using SIS.Application.CourseRegistration.DTOs;

namespace SIS.Application.CourseRegistration.Services;

/// <summary>
/// Öğrencinin "Alınan Dersler" listesini resmî PDF olarak üretir.
/// Üst bilgi bloğu (öğrenci no, ad, sınıf, fakülte, program, dönem, danışman) +
/// dönem bazında tablo + alt toplam (ders sayısı / saat / kredi / AKTS).
/// </summary>
public interface ICourseHistoryPdfService
{
    /// <param name="termId">Belirtilirse yalnız o döneme ait dersler basılır ve üst bloktaki
    /// "Dönemi" alanı o dönemi gösterir; null ise tüm geçmiş + global aktif dönem.</param>
    Task<byte[]> GenerateAsync(int studentId, int? termId = null, CancellationToken cancellationToken = default);
}
