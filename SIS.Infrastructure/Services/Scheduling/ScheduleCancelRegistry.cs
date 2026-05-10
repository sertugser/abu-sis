using System.Collections.Concurrent;
using SIS.Application.Scheduling.Services;

namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// IScheduleCancelRegistry'nin in-memory + thread-safe implementasyonu.
///
/// Tek-instance backend için yeterli; multi-instance senaryosunda Redis-tabanlı bir
/// implementasyona geçilmeli (kullanıcı farklı instance'a düşerse cancel mesajı doğru
/// instance'a yönlendirilmeli). Şu an SignalR Hub aynı process'te çalıştığı için
/// process-local concurrent dictionary güvenli.
/// </summary>
public sealed class ScheduleCancelRegistry : IScheduleCancelRegistry
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeRuns = new();

    public void Register(string userId, CancellationTokenSource cts)
    {
        if (string.IsNullOrEmpty(userId)) return;
        // Aynı userId için eski CTS varsa silelim — yeni run eskiyi override eder.
        if (_activeRuns.TryRemove(userId, out var oldCts))
        {
            try { oldCts.Dispose(); } catch { /* ignore disposed */ }
        }
        _activeRuns[userId] = cts;
    }

    public bool Cancel(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return false;
        if (!_activeRuns.TryGetValue(userId, out var cts)) return false;
        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public void Unregister(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return;
        if (_activeRuns.TryRemove(userId, out var cts))
        {
            try { cts.Dispose(); } catch { /* ignore */ }
        }
    }
}
