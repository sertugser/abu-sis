using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class SystemConfigProvider : ISystemConfigProvider
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

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
        return value != null && decimal.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : fallback;
    }

    public async Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default)
    {
        var value = await GetValueAsync(key, ct);
        return value ?? fallback;
    }

    public async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default)
    {
        var value = await GetValueAsync(key, ct);
        return value != null && bool.TryParse(value, out var result) ? result : fallback;
    }

    private async Task<string?> GetValueAsync(string key, CancellationToken ct)
    {
        var cacheKey = $"SystemConfig:{key}";

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
}
