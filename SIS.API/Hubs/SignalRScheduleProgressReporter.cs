using Microsoft.AspNetCore.SignalR;
using SIS.Application.Scheduling.Services;

namespace SIS.API.Hubs;

/// <summary>
/// IScheduleProgressReporter'ın SignalR implementasyonu.
/// AutoScheduleEngine her aşamada bu sınıfı çağırır; sınıf da
/// IHubContext üzerinden hedef kullanıcıya event push eder.
///
/// Kullanıcı izolasyonu: Clients.User(userId) — JWT'deki NameIdentifier
/// ile eşleşir. Kullanıcının tüm açık sekmelerine paralel olarak yayınlanır.
///
/// no-throw kontratı: hub'a gönderim hata verirse algoritma akışı bundan
/// etkilenmesin — exception swallow edilir, sadece logger'a düşer.
/// </summary>
public sealed class SignalRScheduleProgressReporter : IScheduleProgressReporter
{
    private readonly IHubContext<ScheduleProgressHub> _hub;
    private readonly ILogger<SignalRScheduleProgressReporter> _logger;

    public SignalRScheduleProgressReporter(
        IHubContext<ScheduleProgressHub> hub,
        ILogger<SignalRScheduleProgressReporter> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task ReportAsync(string userId, ScheduleProgressEvent evt, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId)) return;

        try
        {
            await _hub.Clients.User(userId).SendAsync("ScheduleProgress", evt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ScheduleProgress event yayını başarısız (userId={UserId}, stage={Stage})",
                userId, evt.Stage);
        }
    }
}
