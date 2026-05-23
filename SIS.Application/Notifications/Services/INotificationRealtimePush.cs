using SIS.Application.Notifications.DTOs;

namespace SIS.Application.Notifications.Services;

/// <summary>
/// Bildirimi gerçek-zamanlı olarak kullanıcının açık sekmelerine iter (SignalR).
/// Bağımlılık yönü gereği (Infrastructure → API referansı yok) bu arayüz Application'da
/// tanımlanır; SignalR implementasyonu API katmanındadır (IScheduleProgressReporter deseni).
/// no-throw: push hatası ana işlemi (DB insert) bozmamalı.
/// </summary>
public interface INotificationRealtimePush
{
    Task PushAsync(int userId, NotificationDto notification, CancellationToken ct = default);
}
