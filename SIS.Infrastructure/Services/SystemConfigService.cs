using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// SystemConfigs tablosundan konfigürasyon okuyan, IMemoryCache ile önbelleğe alan servis.
/// Cache TTL: 60 saniye. DB'deki değişiklikler 1 dakika içinde yansır.
/// </summary>
public class SystemConfigService : ISystemConfigService
{
    private const string CacheKeyPrefix = "SystemConfig:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public SystemConfigService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue, CancellationToken cancellationToken = default)
    {
        var raw = await GetRawAsync(key, cancellationToken);
        if (raw == null) return defaultValue;
        return int.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue, CancellationToken cancellationToken = default)
    {
        var raw = await GetRawAsync(key, cancellationToken);
        if (raw == null) return defaultValue;
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed : defaultValue;
    }

    public async Task<string> GetStringAsync(string key, string defaultValue, CancellationToken cancellationToken = default)
    {
        var raw = await GetRawAsync(key, cancellationToken);
        return raw ?? defaultValue;
    }

    /// <summary>
    /// CGPA'ya göre ECTS limitini döner.
    /// Keys kullanılır: Registration.EctsLimit.CgpaAbove250 (>=2.50)
    ///                  Registration.EctsLimit.Cgpa200To249 (>=2.00)
    ///                  Registration.EctsLimit.CgpaBelow200 (<2.00)
    /// Varsayılan değerler (DB yoksa): 48, 42, 30
    /// </summary>
    public async Task<int> GetEctsLimitAsync(decimal cgpa, CancellationToken cancellationToken = default)
    {
        if (cgpa >= 2.50m)
            return await GetIntAsync("Registration.EctsLimit.CgpaAbove250", 48, cancellationToken);
        if (cgpa >= 2.00m)
            return await GetIntAsync("Registration.EctsLimit.Cgpa200To249", 42, cancellationToken);
        return await GetIntAsync("Registration.EctsLimit.CgpaBelow200", 30, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────
    //  Dahili önbellekli okuma
    // ─────────────────────────────────────────────────────────────

    private async Task<string?> GetRawAsync(string key, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeyPrefix + key;

        if (_cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        var value = await _context.SystemConfigs
            .AsNoTracking()
            .Where(sc => sc.Key == key)
            .Select(sc => sc.Value)
            .FirstOrDefaultAsync(cancellationToken);

        // Null olsa bile önbelleğe al (DB'ye gereksiz gidişi önler)
        _cache.Set(cacheKey, value, CacheDuration);

        return value;
    }
}
