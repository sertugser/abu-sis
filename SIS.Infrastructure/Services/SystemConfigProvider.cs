using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;
using System.Globalization;

namespace SIS.Infrastructure.Services;

public class SystemConfigProvider : ISystemConfigProvider
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const NumberStyles DecimalStyle = NumberStyles.AllowLeadingWhite
        | NumberStyles.AllowTrailingWhite
        | NumberStyles.AllowLeadingSign
        | NumberStyles.AllowDecimalPoint;

    public SystemConfigProvider(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
    {
        var value = await GetValueAsync(key, ct);
        return value != null && int.TryParse(value, out var result) ? result : fallback;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
    {
        var value = await GetValueAsync(key, ct);
        return value != null && decimal.TryParse(value, DecimalStyle, CultureInfo.InvariantCulture, out var result) ? result : fallback;
    }

    public async Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default)
    {
        var value = await GetValueAsync(key, ct);
        return value ?? fallback;
    }

    public async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default)
    {
        var value = await GetValueAsync(key, ct);
        return TryParseBool(value, out var result) ? result : fallback;
    }

    public void InvalidateCache(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        _cache.Remove(GetCacheKey(key));
    }

    private async Task<string?> GetValueAsync(string key, CancellationToken ct)
    {
        var cacheKey = GetCacheKey(key);

        if (_cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        var value = await _context.SystemConfigs
            .AsNoTracking()
            .Where(c => c.Key == key)
            .Select(c => c.Value)
            .FirstOrDefaultAsync(ct);

        _cache.Set(cacheKey, value, CacheDuration);
        return value;
    }

    private static string GetCacheKey(string key) => $"SystemConfig:{key}";

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
}
