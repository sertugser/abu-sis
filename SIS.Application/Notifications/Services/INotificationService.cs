using SIS.Application.Admin.DTOs;
using SIS.Application.Notifications.DTOs;
using SIS.Domain.Enums;

namespace SIS.Application.Notifications.Services;

/// <summary>
/// Kişisel bildirimlerin tek üretim/okuma kapısı. Domain servisleri (kayıt onayı vb.)
/// olay sonrası <see cref="NotifyAsync"/> çağırır; UI/API okuma metotlarını kullanır.
/// Üretim sonrası gerçek-zamanlı push (SignalR) servis içinde tetiklenir.
/// </summary>
public interface INotificationService
{
    Task NotifyAsync(
        int userId,
        NotificationType type,
        string title,
        string body,
        string? link = null,
        NotificationSeverity severity = NotificationSeverity.Info,
        string? entityRef = null,
        string? groupKey = null,
        DateTime? expiresAt = null,
        CancellationToken ct = default);

    Task NotifyManyAsync(
        IEnumerable<int> userIds,
        NotificationType type,
        string title,
        string body,
        string? link = null,
        NotificationSeverity severity = NotificationSeverity.Info,
        CancellationToken ct = default);

    Task<PagedResult<NotificationDto>> GetForUserAsync(int userId, NotificationFilter filter, CancellationToken ct = default);

    /// <summary>Zil önizlemesi için en güncel N bildirim.</summary>
    Task<IReadOnlyList<NotificationDto>> GetRecentAsync(int userId, int max, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default);

    Task MarkReadAsync(int userId, int id, CancellationToken ct = default);
    Task MarkAllReadAsync(int userId, CancellationToken ct = default);
    Task DeleteAsync(int userId, int id, CancellationToken ct = default);
}
