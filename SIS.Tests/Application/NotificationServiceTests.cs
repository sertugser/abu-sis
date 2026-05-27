using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SIS.Application.Notifications.DTOs;
using SIS.Application.Notifications.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class NotificationServiceTests
{
    private sealed class RecordingPush : INotificationRealtimePush
    {
        public List<(int UserId, NotificationDto Dto)> Pushed { get; } = new();
        public bool Throw { get; set; }

        public Task PushAsync(int userId, NotificationDto notification, CancellationToken ct = default)
        {
            if (Throw)
                throw new InvalidOperationException("hub down");
            Pushed.Add((userId, notification));
            return Task.CompletedTask;
        }
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"NotifTest-{Guid.NewGuid()}")
            .Options);

    private static NotificationService CreateService(AppDbContext ctx, RecordingPush push, DateTime? now = null) =>
        new(ctx,
            new FakeClock { UtcNow = now ?? new DateTime(2026, 5, 23, 12, 0, 0, DateTimeKind.Utc) },
            push,
            NullLogger<NotificationService>.Instance);

    [Fact]
    public async Task NotifyAsync_PersistsNotification_AndPushes()
    {
        using var ctx = CreateContext();
        var push = new RecordingPush();
        var svc = CreateService(ctx, push);

        await svc.NotifyAsync(42, NotificationType.RegistrationApproved, "Onaylandı", "Ders kaydınız onaylandı.", link: "/alinan-dersler", severity: NotificationSeverity.Success);

        var stored = ctx.Notifications.Single();
        stored.UserId.Should().Be(42);
        stored.Type.Should().Be(NotificationType.RegistrationApproved);
        stored.IsRead.Should().BeFalse();
        push.Pushed.Should().ContainSingle();
        push.Pushed[0].UserId.Should().Be(42);
        push.Pushed[0].Dto.Title.Should().Be("Onaylandı");
    }

    [Fact]
    public async Task NotifyAsync_DoesNotThrow_WhenPushFails_AndStillPersists()
    {
        using var ctx = CreateContext();
        var push = new RecordingPush { Throw = true };
        var svc = CreateService(ctx, push);

        var act = () => svc.NotifyAsync(42, NotificationType.RegistrationRejected, "Reddedildi", "...");

        await act.Should().NotThrowAsync();
        ctx.Notifications.Count(n => n.UserId == 42).Should().Be(1);
    }

    [Fact]
    public async Task GetUnreadCountAsync_CountsOnlyUnread_AndExcludesExpired()
    {
        using var ctx = CreateContext();
        var push = new RecordingPush();
        var now = new DateTime(2026, 5, 23, 12, 0, 0, DateTimeKind.Utc);
        var svc = CreateService(ctx, push, now);

        await svc.NotifyAsync(1, NotificationType.RegistrationApproved, "a", "a");
        await svc.NotifyAsync(1, NotificationType.RegistrationApproved, "b", "b");
        await svc.NotifyAsync(1, NotificationType.RegistrationApproved, "expired", "x", expiresAt: now.AddMinutes(-1));
        await svc.NotifyAsync(2, NotificationType.RegistrationApproved, "other-user", "y");

        (await svc.GetUnreadCountAsync(1)).Should().Be(2); // expired hariç, başka kullanıcı hariç
    }

    [Fact]
    public async Task GetForUserAsync_FiltersByTypeAndUnread_AndScopesToUser()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx, new RecordingPush());

        await svc.NotifyAsync(1, NotificationType.RegistrationApproved, "approved", "a");
        await svc.NotifyAsync(1, NotificationType.RegistrationRejected, "rejected", "b");
        await svc.NotifyAsync(2, NotificationType.RegistrationApproved, "other", "c");

        var result = await svc.GetForUserAsync(1, new NotificationFilter(Type: "RegistrationApproved"));

        result.Items.Should().ContainSingle();
        result.Items[0].Title.Should().Be("approved");
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task MarkReadAsync_OnlyAffectsOwnNotification()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx, new RecordingPush());

        await svc.NotifyAsync(1, NotificationType.RegistrationApproved, "mine", "a");
        var mine = ctx.Notifications.Single(n => n.UserId == 1);

        // Başka kullanıcı (2) okumaya çalışırsa değişmez.
        await svc.MarkReadAsync(userId: 2, id: mine.Id);
        ctx.Notifications.Single(n => n.Id == mine.Id).IsRead.Should().BeFalse();

        // Sahibi okur.
        await svc.MarkReadAsync(userId: 1, id: mine.Id);
        ctx.Notifications.Single(n => n.Id == mine.Id).IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAllReadAsync_MarksEveryUnreadForUser()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx, new RecordingPush());

        await svc.NotifyAsync(1, NotificationType.RegistrationApproved, "a", "a");
        await svc.NotifyAsync(1, NotificationType.RegistrationApproved, "b", "b");

        await svc.MarkAllReadAsync(1);

        ctx.Notifications.Where(n => n.UserId == 1).All(n => n.IsRead).Should().BeTrue();
        (await svc.GetUnreadCountAsync(1)).Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOwn_ButNotOthers()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx, new RecordingPush());

        await svc.NotifyAsync(1, NotificationType.RegistrationApproved, "mine", "a");
        var mine = ctx.Notifications.Single(n => n.UserId == 1);

        await svc.DeleteAsync(userId: 2, id: mine.Id); // yabancı silemez
        ctx.Notifications.Count(n => n.Id == mine.Id).Should().Be(1);

        await svc.DeleteAsync(userId: 1, id: mine.Id);
        ctx.Notifications.Count(n => n.Id == mine.Id).Should().Be(0);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsLatestFirst_LimitedToMax()
    {
        using var ctx = CreateContext();
        var now = new DateTime(2026, 5, 23, 12, 0, 0, DateTimeKind.Utc);
        var clock = new FakeClock { UtcNow = now };
        var svc = new NotificationService(ctx, clock, new RecordingPush(), NullLogger<NotificationService>.Instance);

        clock.UtcNow = now; await svc.NotifyAsync(1, NotificationType.RegistrationApproved, "old", "a");
        clock.UtcNow = now.AddMinutes(1); await svc.NotifyAsync(1, NotificationType.RegistrationApproved, "mid", "b");
        clock.UtcNow = now.AddMinutes(2); await svc.NotifyAsync(1, NotificationType.RegistrationApproved, "new", "c");

        var recent = await svc.GetRecentAsync(1, max: 2);

        recent.Should().HaveCount(2);
        recent[0].Title.Should().Be("new");
        recent[1].Title.Should().Be("mid");
    }
}
