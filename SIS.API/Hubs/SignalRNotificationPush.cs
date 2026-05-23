using Microsoft.AspNetCore.SignalR;
using SIS.Application.Notifications.DTOs;
using SIS.Application.Notifications.Services;

namespace SIS.API.Hubs;

/// <summary>
/// <see cref="INotificationRealtimePush"/>'ın SignalR implementasyonu (API katmanında —
/// IHubContext burada erişilebilir). NotificationService DB'ye yazdıktan sonra çağırır.
/// no-throw: push hatası ana akışı bozmasın (SignalRScheduleProgressReporter deseni).
/// </summary>
public sealed class SignalRNotificationPush : INotificationRealtimePush
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<SignalRNotificationPush> _logger;

    public SignalRNotificationPush(IHubContext<NotificationHub> hub, ILogger<SignalRNotificationPush> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task PushAsync(int userId, NotificationDto notification, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.User(userId.ToString()).SendAsync("Notification", notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification push başarısız (userId={UserId}, type={Type})", userId, notification.Type);
        }
    }
}
