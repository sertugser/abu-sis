namespace SIS.Application.Common.Services;

/// <summary>
/// Merkezi sistem konfigürasyonuna erişim sağlar.
/// Değerler önbelleğe alınır; DB'deki değişiklikler 1 dakika içinde yansır.
/// </summary>
public interface ISystemConfigService
{
    /// <summary>SystemConfig tablosundan int değer okur.</summary>
    Task<int> GetIntAsync(string key, int defaultValue, CancellationToken cancellationToken = default);

    /// <summary>SystemConfig tablosundan decimal değer okur.</summary>
    Task<decimal> GetDecimalAsync(string key, decimal defaultValue, CancellationToken cancellationToken = default);

    /// <summary>SystemConfig tablosundan string değer okur.</summary>
    Task<string> GetStringAsync(string key, string defaultValue, CancellationToken cancellationToken = default);

    /// <summary>CGPA'ya göre ECTS limitini döner (Registration.EctsLimit.* key'lerinden).</summary>
    Task<int> GetEctsLimitAsync(decimal cgpa, CancellationToken cancellationToken = default);
}
