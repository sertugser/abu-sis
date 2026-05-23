namespace SIS.Application.Notifications.DTOs;

/// <summary>Frontend'e dönen bildirim. Type/Severity enum adı (string) olarak gider → ikon/renk eşlemesi.</summary>
public record NotificationDto(
    int Id,
    string Type,
    string Severity,
    string Title,
    string Body,
    string? Link,
    bool IsRead,
    DateTime CreatedAt
);

/// <summary>/bildirimler sayfası listeleme filtresi.</summary>
public record NotificationFilter(
    string? Type = null,
    bool OnlyUnread = false,
    int Page = 1,
    int PageSize = 20
);
