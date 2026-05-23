using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIS.Application.Admin.DTOs;
using SIS.Application.Common.Time;
using SIS.Application.Notifications.DTOs;
using SIS.Application.Notifications.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _ctx;
    private readonly IDateTimeProvider _clock;
    private readonly INotificationRealtimePush _push;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AppDbContext ctx,
        IDateTimeProvider clock,
        INotificationRealtimePush push,
        ILogger<NotificationService> logger)
    {
        _ctx = ctx;
        _clock = clock;
        _push = push;
        _logger = logger;
    }

    public async Task NotifyAsync(
        int userId,
        NotificationType type,
        string title,
        string body,
        string? link = null,
        NotificationSeverity severity = NotificationSeverity.Info,
        string? entityRef = null,
        string? groupKey = null,
        DateTime? expiresAt = null,
        CancellationToken ct = default)
    {
        var entity = new Notification
        {
            UserId = userId,
            Type = type,
            Severity = severity,
            Title = title,
            Body = body,
            Link = link,
            EntityRef = entityRef,
            GroupKey = groupKey,
            ExpiresAt = expiresAt,
            IsRead = false,
            CreatedAt = _clock.UtcNow,
        };

        _ctx.Notifications.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        await PushSafelyAsync(userId, ToDto(entity), ct);
    }

    public async Task NotifyManyAsync(
        IEnumerable<int> userIds,
        NotificationType type,
        string title,
        string body,
        string? link = null,
        NotificationSeverity severity = NotificationSeverity.Info,
        CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var distinctIds = userIds.Distinct().ToList();
        if (distinctIds.Count == 0)
            return;

        var entities = distinctIds.Select(userId => new Notification
        {
            UserId = userId,
            Type = type,
            Severity = severity,
            Title = title,
            Body = body,
            Link = link,
            IsRead = false,
            CreatedAt = now,
        }).ToList();

        _ctx.Notifications.AddRange(entities);
        await _ctx.SaveChangesAsync(ct);

        foreach (var entity in entities)
            await PushSafelyAsync(entity.UserId, ToDto(entity), ct);
    }

    public async Task<PagedResult<NotificationDto>> GetForUserAsync(int userId, NotificationFilter filter, CancellationToken ct = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize);

        var query = ActiveForUser(userId);

        if (filter.OnlyUnread)
            query = query.Where(n => !n.IsRead);

        if (!string.IsNullOrWhiteSpace(filter.Type) && Enum.TryParse<NotificationType>(filter.Type, ignoreCase: true, out var type))
            query = query.Where(n => n.Type == type);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => ToDto(n))
            .ToListAsync(ct);

        return new PagedResult<NotificationDto>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetRecentAsync(int userId, int max, CancellationToken ct = default)
    {
        var take = Math.Clamp(max, 1, MaxPageSize);
        return await ActiveForUser(userId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(take)
            .Select(n => ToDto(n))
            .ToListAsync(ct);
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default) =>
        ActiveForUser(userId).Where(n => !n.IsRead).CountAsync(ct);

    public async Task MarkReadAsync(int userId, int id, CancellationToken ct = default)
    {
        var entity = await _ctx.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        if (entity is null || entity.IsRead)
            return;

        entity.IsRead = true;
        entity.ReadAt = _clock.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var unread = await _ctx.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(ct);
        if (unread.Count == 0)
            return;

        foreach (var entity in unread)
        {
            entity.IsRead = true;
            entity.ReadAt = now;
        }

        await _ctx.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int userId, int id, CancellationToken ct = default)
    {
        var entity = await _ctx.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        if (entity is null)
            return;

        _ctx.Notifications.Remove(entity);
        await _ctx.SaveChangesAsync(ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    // Süresi geçmiş bildirimler gizlenir (kalıcı silme Faz 2 prune job'una bırakıldı).
    private IQueryable<Notification> ActiveForUser(int userId)
    {
        var now = _clock.UtcNow;
        return _ctx.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && (n.ExpiresAt == null || n.ExpiresAt > now));
    }

    private async Task PushSafelyAsync(int userId, NotificationDto dto, CancellationToken ct)
    {
        try
        {
            await _push.PushAsync(userId, dto, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bildirim realtime push başarısız (userId={UserId}, type={Type})", userId, dto.Type);
        }
    }

    private static NotificationDto ToDto(Notification n) =>
        new(n.Id, n.Type.ToString(), n.Severity.ToString(), n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt);
}
