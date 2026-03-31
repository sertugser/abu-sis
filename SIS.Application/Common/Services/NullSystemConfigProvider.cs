namespace SIS.Application.Common.Services;

/// <summary>
/// Null-object implementation of ISystemConfigProvider.
/// Always returns the fallback value without hitting the database.
/// Used when no real provider is injected (e.g. in tests).
/// </summary>
public sealed class NullSystemConfigProvider : ISystemConfigProvider
{
    public Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
        => Task.FromResult(fallback);

    public Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
        => Task.FromResult(fallback);

    public Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default)
        => Task.FromResult(fallback);

    public Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default)
        => Task.FromResult(fallback);
}
