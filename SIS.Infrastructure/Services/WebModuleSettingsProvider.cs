using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class WebModuleSettingsProvider : IWebModuleSettingsProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IDateTimeProvider _clock;

    public WebModuleSettingsProvider(AppDbContext context, IMemoryCache cache, IDateTimeProvider clock)
    {
        _context = context;
        _cache = cache;
        _clock = clock;
    }

    public async Task<bool> GetBoolAsync(string moduleKey, string key, bool fallback, CancellationToken ct = default)
    {
        var value = await GetActiveValueAsync(moduleKey, key, ct);
        return TryParseBool(value, out var result) ? result : fallback;
    }

    public async Task<int> GetIntAsync(string moduleKey, string key, int fallback, CancellationToken ct = default)
    {
        var value = await GetActiveValueAsync(moduleKey, key, ct);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : fallback;
    }

    public async Task<string> GetStringAsync(string moduleKey, string key, string fallback, CancellationToken ct = default)
    {
        var value = await GetActiveValueAsync(moduleKey, key, ct);
        return value ?? fallback;
    }

    public async Task<TimeOnly> GetTimeAsync(string moduleKey, string key, TimeOnly fallback, CancellationToken ct = default)
    {
        var value = await GetActiveValueAsync(moduleKey, key, ct);
        return TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
            ? result
            : fallback;
    }

    public void InvalidateCache(string moduleKey, string key)
    {
        if (string.IsNullOrWhiteSpace(moduleKey) || string.IsNullOrWhiteSpace(key))
            return;

        _cache.Remove(GetCacheKey(moduleKey, key));
    }

    private async Task<string?> GetActiveValueAsync(string moduleKey, string key, CancellationToken ct)
    {
        var setting = await GetSettingAsync(moduleKey, key, ct);
        if (setting is null)
            return null;

        var now = _clock.UtcNow;
        if (setting.EffectiveFrom.HasValue && now < setting.EffectiveFrom.Value)
            return null;
        if (setting.EffectiveTo.HasValue && now >= setting.EffectiveTo.Value)
            return null;

        return setting.Value;
    }

    private async Task<WebModuleSettingValue?> GetSettingAsync(string moduleKey, string key, CancellationToken ct)
    {
        var cacheKey = GetCacheKey(moduleKey, key);

        if (_cache.TryGetValue(cacheKey, out WebModuleSettingValue? cached))
            return cached;

        var setting = await _context.WebModuleSettings
            .AsNoTracking()
            .Where(s => s.ModuleKey == moduleKey && s.Key == key)
            .Select(s => new WebModuleSettingValue(s.Value, s.EffectiveFrom, s.EffectiveTo))
            .FirstOrDefaultAsync(ct);

        _cache.Set(cacheKey, setting, CacheDuration);
        return setting;
    }

    private static string GetCacheKey(string moduleKey, string key) => $"WebModuleSetting:{moduleKey}:{key}";

    private static bool TryParseBool(string? value, out bool result)
    {
        result = false;

        var normalized = value?.Trim().ToLowerInvariant();
        if (normalized is null)
            return false;

        if (normalized is "true" or "1" or "yes" or "on")
        {
            result = true;
            return true;
        }

        if (normalized is "false" or "0" or "no" or "off")
        {
            result = false;
            return true;
        }

        return false;
    }

    private sealed record WebModuleSettingValue(string Value, DateTime? EffectiveFrom, DateTime? EffectiveTo);
}
