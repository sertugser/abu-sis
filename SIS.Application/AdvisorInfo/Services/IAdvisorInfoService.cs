using SIS.Application.AdvisorInfo.DTOs;

namespace SIS.Application.AdvisorInfo.Services;

public interface IAdvisorInfoService
{
    /// <summary>
    /// Verilen kullanıcı ID'sine (öğrenci) ait danışman bilgilerini döner.
    /// Öğrenci kaydı yoksa veya danışman atanmamışsa <c>HasAdvisor=false</c> ile boş DTO döner.
    /// </summary>
    Task<AdvisorInfoDto> GetMyAdvisorAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Verilen kullanıcı ID'sine (öğrenci) ait danışmanın aktif dönem ders programını döner.
    /// Öğrenci/danışman/aktif dönem yoksa boş liste döner (TermLabel null).
    /// Öğrenci kaydı YOKSA <see cref="KeyNotFoundException"/> fırlatır (controller 404'e çevirir).
    /// Danışman ATANMAMIŞSA <see cref="InvalidOperationException"/> fırlatır (controller 404'e çevirir).
    /// </summary>
    Task<AdvisorScheduleDto> GetAdvisorScheduleAsync(int userId, CancellationToken ct = default);
}
