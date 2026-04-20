using SIS.Application.UserPreferences.DTOs;

namespace SIS.Application.UserPreferences.Services;

public interface IUserPreferenceService
{
    /// <summary>Kullanici tercihini getirir. Kayit yoksa bos favori listesiyle dondurur.</summary>
    Task<UserPreferenceDto> GetAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Kullanici tercihini gunceller veya olusturur.</summary>
    /// <exception cref="ArgumentException">Favori sayisi limite ulasirsa veya route gecersizse.</exception>
    Task<UserPreferenceDto> UpdateAsync(
        int userId,
        UpdateUserPreferenceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Favori listesi icin izin verilen maksimum oge sayisi.</summary>
    int MaxFavorites { get; }
}
