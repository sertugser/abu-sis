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

public class EvaluationPeriodOpenerJobTests
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

    private static (AppDbContext ctx, FakeClock clock) Build(DateTime now)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"evalopen-{Guid.NewGuid()}").Options;
        return (new AppDbContext(opts), new FakeClock(now));
    }

    private static AcademicTerm TermReady(DateTime now) => new()
    {
        Id = 1, Name = "T",
        RegistrationStart = now.AddDays(-30), RegistrationEnd = now.AddDays(-20),
        AddDropStart = now.AddDays(-20), AddDropEnd = now.AddDays(-15),
        TermStart = now.AddDays(-60), TermEnd = now.AddDays(30),
        WithdrawalDeadline = now.AddDays(-10),
        GradeEntryStart = now.AddDays(-1), GradeEntryEnd = now.AddDays(20)
    };

    [Fact]
    public async Task Opens_WhenGradeEntryStartPassed_AndNoneOpen()
    {
        var now = DateTime.UtcNow;
        var (ctx, clock) = Build(now);
        ctx.AcademicTerms.Add(TermReady(now));
        await ctx.SaveChangesAsync();

        var job = new EvaluationPeriodOpenerJob(new TestScopeFactory(ctx), clock, NullLogger<EvaluationPeriodOpenerJob>.Instance);
        await job.RunOnceAsync(CancellationToken.None);

        var periods = await ctx.EvaluationPeriods.ToListAsync();
        periods.Should().ContainSingle();
        periods[0].Status.Should().Be(EvaluationPeriodStatus.Open);
    }

    [Fact]
    public async Task DoesNothing_WhenAlreadyOpen()
    {
        var now = DateTime.UtcNow;
        var (ctx, clock) = Build(now);
        ctx.AcademicTerms.Add(TermReady(now));
        ctx.EvaluationPeriods.Add(new EvaluationPeriod { TermId = 1, Status = EvaluationPeriodStatus.Open, OpenedAt = now.AddDays(-1), ClosesAt = now.AddDays(20) });
        await ctx.SaveChangesAsync();

        var job = new EvaluationPeriodOpenerJob(new TestScopeFactory(ctx), clock, NullLogger<EvaluationPeriodOpenerJob>.Instance);
        await job.RunOnceAsync(CancellationToken.None);

        (await ctx.EvaluationPeriods.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DoesNothing_WhenBeforeGradeEntryStart()
    {
        var now = DateTime.UtcNow;
        var (ctx, clock) = Build(now);
        var term = TermReady(now);
        term.GradeEntryStart = now.AddDays(10);
        ctx.AcademicTerms.Add(term);
        await ctx.SaveChangesAsync();

        var job = new EvaluationPeriodOpenerJob(new TestScopeFactory(ctx), clock, NullLogger<EvaluationPeriodOpenerJob>.Instance);
        await job.RunOnceAsync(CancellationToken.None);

        (await ctx.EvaluationPeriods.CountAsync()).Should().Be(0);
    }
}
