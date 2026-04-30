namespace SIS.Application.Common.Services;

/// <summary>
/// Web modüllerine ait yerel davranış ayarlarını okur.
/// EffectiveFrom/EffectiveTo aralığı dışında kalan ayarlar fallback değere düşer.
/// </summary>
public interface IWebModuleSettingsProvider
{
    Task<bool> GetBoolAsync(string moduleKey, string key, bool fallback, CancellationToken ct = default);
    Task<int> GetIntAsync(string moduleKey, string key, int fallback, CancellationToken ct = default);
    Task<string> GetStringAsync(string moduleKey, string key, string fallback, CancellationToken ct = default);
    Task<TimeOnly> GetTimeAsync(string moduleKey, string key, TimeOnly fallback, CancellationToken ct = default);
    void InvalidateCache(string moduleKey, string key);
}
