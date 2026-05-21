using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.BackgroundServices;
using SIS.Infrastructure.Persistence;
using Xunit;

namespace SIS.Tests.Infrastructure;

public class AcademicTermActivationReminderJobTests
{
    private sealed class FakeClock(DateTime now) : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = now;
    }

    private sealed class TestScopeFactory(AppDbContext ctx) : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton(ctx);
            return sc.BuildServiceProvider().CreateScope();
        }
    }

    private static (AppDbContext ctx, FakeClock clock) BuildContext(DateTime now)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"term-reminder-{Guid.NewGuid()}")
            .Options;
        var ctx = new AppDbContext(opts);
        var clock = new FakeClock(now);
        return (ctx, clock);
    }

    [Fact]
    public async Task CreatesManagementAnnouncement_WhenInactiveTermStartsWithinTenDays()
    {
        var now = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc);
        var (ctx, clock) = BuildContext(now);
        ctx.AcademicTerms.Add(new AcademicTerm
        {
            Id = 1,
            Name = "2026-2027 Güz",
            Year = 2026,
            TermType = TermType.Fall,
            IsActivated = false,
            IsCancelled = false,
            TermStart = now.Date.AddDays(10),
            TermEnd = now.Date.AddDays(120),
            CreatedAt = now,
        });
        await ctx.SaveChangesAsync();

        var job = new AcademicTermActivationReminderJob(
            new TestScopeFactory(ctx),
            clock,
            NullLogger<AcademicTermActivationReminderJob>.Instance);

        await job.RunOnceAsync(CancellationToken.None);

        var announcement = await ctx.InternalAnnouncements.SingleAsync();
        announcement.Title.Should().Be("Akademik dönem aktivasyonu gerekli: 2026-2027 Güz");
        announcement.TargetAudience.Should().Be(AnnouncementAudience.Management);
        announcement.IsPinned.Should().BeTrue();
        announcement.CreatedBy.Should().Be(AcademicTermActivationReminderJob.SystemCreatedBy);
        announcement.Content.Should().Contain("başlamasına 10 gün kaldı");
    }

    [Fact]
    public async Task IsIdempotent_ForSameTerm()
    {
        var now = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc);
        var (ctx, clock) = BuildContext(now);
        ctx.AcademicTerms.Add(new AcademicTerm
        {
            Id = 1,
            Name = "2026-2027 Güz",
            Year = 2026,
            TermType = TermType.Fall,
            IsActivated = false,
            TermStart = now.Date.AddDays(3),
            TermEnd = now.Date.AddDays(120),
            CreatedAt = now,
        });
        await ctx.SaveChangesAsync();

        var job = new AcademicTermActivationReminderJob(
            new TestScopeFactory(ctx),
            clock,
            NullLogger<AcademicTermActivationReminderJob>.Instance);

        await job.RunOnceAsync(CancellationToken.None);
        await job.RunOnceAsync(CancellationToken.None);

        (await ctx.InternalAnnouncements.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DoesNothing_ForActivatedCancelledOrFarFutureTerms()
    {
        var now = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc);
        var (ctx, clock) = BuildContext(now);
        ctx.AcademicTerms.AddRange(
            new AcademicTerm
            {
                Id = 1,
                Name = "Aktif Aday",
                IsActivated = true,
                TermStart = now.Date.AddDays(5),
                TermEnd = now.Date.AddDays(100),
                CreatedAt = now,
            },
            new AcademicTerm
            {
                Id = 2,
                Name = "İptal Dönem",
                IsActivated = false,
                IsCancelled = true,
                TermStart = now.Date.AddDays(5),
                TermEnd = now.Date.AddDays(100),
                CreatedAt = now,
            },
            new AcademicTerm
            {
                Id = 3,
                Name = "Uzak Dönem",
                IsActivated = false,
                TermStart = now.Date.AddDays(11),
                TermEnd = now.Date.AddDays(100),
                CreatedAt = now,
            });
        await ctx.SaveChangesAsync();

        var job = new AcademicTermActivationReminderJob(
            new TestScopeFactory(ctx),
            clock,
            NullLogger<AcademicTermActivationReminderJob>.Instance);

        await job.RunOnceAsync(CancellationToken.None);

        (await ctx.InternalAnnouncements.CountAsync()).Should().Be(0);
    }
}
