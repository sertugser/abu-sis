using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SIS.API.Hubs;

/// <summary>
/// Kişisel bildirimlerin gerçek-zamanlı push hub'ı. Tek yön (sunucu → istemci):
/// NotificationService bir bildirim üretince <c>Clients.User(userId)</c>'a "Notification"
/// event'i yayınlar; istemci yalnızca dinler.
///
/// Kullanıcı izolasyonu ScheduleProgressHub ile aynı: JWT'deki NameIdentifier = userId,
/// bu yüzden kullanıcının tüm açık sekmelerine paralel push edilir.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
}
