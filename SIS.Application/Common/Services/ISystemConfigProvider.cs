namespace SIS.Application.Common.Services;

/// <summary>
/// SystemConfig tablosundan konfigürasyon değerlerini okur.
/// Servisler hardcoded değer yerine bu provider'ı kullanır.
/// </summary>
public interface ISystemConfigProvider
{
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default);
    Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default);
}
