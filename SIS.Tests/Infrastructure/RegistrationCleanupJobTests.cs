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

public class RegistrationCleanupJobTests
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
            .UseInMemoryDatabase($"regcleanup-{Guid.NewGuid()}")
            .Options;
        var ctx = new AppDbContext(opts);
        var clock = new FakeClock(now);
        return (ctx, clock);
    }

    [Fact]
    public async Task CleansDraftRegistrations_WhenAddDropStartPassed()
    {
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var (ctx, clock) = BuildContext(now);

        var term = new AcademicTerm
        {
            Id = 1, Name = "2026 Güz",
            RegistrationStart = now.AddDays(-14), RegistrationEnd = now.AddDays(-5),
            AddDropStart = now.AddDays(-1), AddDropEnd = now.AddDays(5),
            TermStart = now.AddDays(-1), TermEnd = now.AddDays(100),
            WithdrawalDeadline = now.AddDays(60),
            GradeEntryStart = now.AddDays(90), GradeEntryEnd = now.AddDays(110)
        };
        ctx.AcademicTerms.Add(term);
        var section = new CourseSection { Id = 1, TermId = 1, EnrolledCount = 2, Status = CourseSectionStatus.Active, CourseId = 1 };
        ctx.CourseSections.Add(section);
        ctx.CourseRegistrations.AddRange(
            new CourseRegistration { Id = 1, CourseSectionId = 1, StudentId = 1, Status = RegistrationStatus.Draft },
            new CourseRegistration { Id = 2, CourseSectionId = 1, StudentId = 2, Status = RegistrationStatus.PendingAdvisorApproval }
        );
        await ctx.SaveChangesAsync();

        var job = new RegistrationCleanupJob(new TestScopeFactory(ctx), clock, NullLogger<RegistrationCleanupJob>.Instance);
        await job.RunOnceAsync(CancellationToken.None);

        var updated = await ctx.CourseRegistrations.ToListAsync();
        updated.Should().OnlyContain(r => r.Status == RegistrationStatus.Dropped);
        (await ctx.CourseSections.FindAsync(1))!.EnrolledCount.Should().Be(0);
    }

    [Fact]
    public async Task DoesNothing_WhenBeforeAddDropStart()
    {
        var now = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
        var (ctx, clock) = BuildContext(now);
        var term = new AcademicTerm
        {
            Id = 1, Name = "T",
            RegistrationStart = now.AddDays(-5), RegistrationEnd = now.AddDays(5),
            AddDropStart = now.AddDays(10), AddDropEnd = now.AddDays(20),
            TermStart = now, TermEnd = now.AddDays(100),
            WithdrawalDeadline = now.AddDays(60),
            GradeEntryStart = now.AddDays(90), GradeEntryEnd = now.AddDays(110)
        };
        ctx.AcademicTerms.Add(term);
        ctx.CourseRegistrations.Add(
            new CourseRegistration { Id = 1, CourseSectionId = 1, StudentId = 1, Status = RegistrationStatus.Draft });
        ctx.CourseSections.Add(new CourseSection { Id = 1, TermId = 1, EnrolledCount = 1, Status = CourseSectionStatus.Active, CourseId = 1 });
        await ctx.SaveChangesAsync();

        var job = new RegistrationCleanupJob(new TestScopeFactory(ctx), clock, NullLogger<RegistrationCleanupJob>.Instance);
        await job.RunOnceAsync(CancellationToken.None);

        (await ctx.CourseRegistrations.FindAsync(1))!.Status.Should().Be(RegistrationStatus.Draft);
    }

    [Fact]
    public async Task IsIdempotent()
    {
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var (ctx, clock) = BuildContext(now);
        var term = new AcademicTerm
        {
            Id = 1, Name = "T",
            RegistrationStart = now.AddDays(-14), RegistrationEnd = now.AddDays(-5),
            AddDropStart = now.AddDays(-1), AddDropEnd = now.AddDays(5),
            TermStart = now.AddDays(-1), TermEnd = now.AddDays(100),
            WithdrawalDeadline = now.AddDays(60),
            GradeEntryStart = now.AddDays(90), GradeEntryEnd = now.AddDays(110)
        };
        ctx.AcademicTerms.Add(term);
        ctx.CourseSections.Add(new CourseSection { Id = 1, TermId = 1, EnrolledCount = 1, Status = CourseSectionStatus.Active, CourseId = 1 });
        ctx.CourseRegistrations.Add(new CourseRegistration { Id = 1, CourseSectionId = 1, StudentId = 1, Status = RegistrationStatus.Draft });
        await ctx.SaveChangesAsync();

        var job = new RegistrationCleanupJob(new TestScopeFactory(ctx), clock, NullLogger<RegistrationCleanupJob>.Instance);
        await job.RunOnceAsync(CancellationToken.None);
        await job.RunOnceAsync(CancellationToken.None);

        (await ctx.CourseSections.FindAsync(1))!.EnrolledCount.Should().Be(0);
    }
}
