namespace SIS.Application.Common.Services;

/// <summary>
/// Testler ve opsiyonel entegrasyonlar için null-object provider.
/// Her zaman fallback değeri döner.
/// </summary>
public sealed class NullWebModuleSettingsProvider : IWebModuleSettingsProvider
{
    public Task<bool> GetBoolAsync(string moduleKey, string key, bool fallback, CancellationToken ct = default)
        => Task.FromResult(fallback);

    public Task<int> GetIntAsync(string moduleKey, string key, int fallback, CancellationToken ct = default)
        => Task.FromResult(fallback);

    public Task<string> GetStringAsync(string moduleKey, string key, string fallback, CancellationToken ct = default)
        => Task.FromResult(fallback);

    public Task<TimeOnly> GetTimeAsync(string moduleKey, string key, TimeOnly fallback, CancellationToken ct = default)
        => Task.FromResult(fallback);

    public void InvalidateCache(string moduleKey, string key)
    {
    }
}
