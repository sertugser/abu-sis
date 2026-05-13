using SIS.Application.CourseRegistration.DTOs;

namespace SIS.Application.CourseRegistration.Services;

/// <summary>
/// Öğrencinin "Alınan Dersler" listesini resmî PDF olarak üretir.
/// Üst bilgi bloğu (öğrenci no, ad, sınıf, fakülte, program, dönem, danışman) +
/// dönem bazında tablo + alt toplam (ders sayısı / saat / kredi / AKTS).
/// </summary>
public interface ICourseHistoryPdfService
{
    Task<byte[]> GenerateAsync(int studentId, CancellationToken cancellationToken = default);
}
