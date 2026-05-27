using SIS.Application.Common.DTOs;

namespace SIS.Application.Curriculum.Services;

/// <summary>
/// Ders bazlı öğrenci istatistikleri (intibak sayfası detay paneli).
/// </summary>
public interface ICourseStatisticsService
{
    /// <summary>
    /// Tek bir ders için "dersi alan / başarılı / başarısız" sayılarını (toplam + aktif öğrenci kırılımı) döndürür.
    /// </summary>
    Task<CourseIntibakStatisticsDto> GetCourseIntibakStatisticsAsync(int courseId, CancellationToken ct = default);
}
