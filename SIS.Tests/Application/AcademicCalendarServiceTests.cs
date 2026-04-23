using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicCalendar.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class AcademicCalendarServiceTests
{
    // ── Factory helpers ───────────────────────────────────────────────

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AcalTest-{Guid.NewGuid()}")
            .Options);

    private static AcademicCalendarService CreateService(AppDbContext ctx, DateTime? now = null)
    {
        var clock = new FakeClock { UtcNow = now ?? new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc) };
        return new AcademicCalendarService(ctx, clock);
    }

    private static AcademicTerm SeedTerm(AppDbContext ctx, bool cancelled = false)
    {
        var term = new AcademicTerm
        {
            Name = "2025-2026 Güz",
            ShortName = "2025G",
            EnglishName = "2025-2026 Fall",
            ShortEnglishName = "25F",
            Year = 2025,
            TermType = TermType.Fall,
            TermStart = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            TermEnd   = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            IsCancelled = cancelled,
            CreatedAt = DateTime.UtcNow
        };
        ctx.AcademicTerms.Add(term);
        ctx.SaveChanges();
        return term;
    }

    private static UpsertAcademicCalendarDto ValidDto(AcademicTerm t) => new(
        RegistrationStart:  t.TermStart.AddDays(-10),
        RegistrationEnd:    t.TermStart,
        AddDropStart:       t.TermStart,
        AddDropEnd:         t.TermStart.AddDays(14),
        WithdrawalDeadline: t.TermEnd.AddDays(-30),
        GradeEntryStart:    t.TermEnd,
        GradeEntryEnd:      t.TermEnd.AddDays(14));

    // ── Tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByTermIdAsync_ReturnsNull_WhenCalendarMissing()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetByTermIdAsync(term.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTermIdAsync_ThrowsNotFound_WhenTermMissing()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);

        Func<Task> act = async () => await svc.GetByTermIdAsync(999);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpsertAsync_CreatesCalendar_WhenNone()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        var result = await svc.UpsertAsync(term.Id, ValidDto(term));

        result.Should().NotBeNull();
        result.TermId.Should().Be(term.Id);
        ctx.TermAcademicCalendars.Count(c => c.TermId == term.Id).Should().Be(1);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesCalendar_WhenExists()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        // First upsert
        await svc.UpsertAsync(term.Id, ValidDto(term));

        // Second upsert with different dates
        var newDto = new UpsertAcademicCalendarDto(
            RegistrationStart:  term.TermStart.AddDays(-15),
            RegistrationEnd:    term.TermStart.AddDays(-1),
            AddDropStart:       term.TermStart,
            AddDropEnd:         term.TermStart.AddDays(10),
            WithdrawalDeadline: term.TermEnd.AddDays(-40),
            GradeEntryStart:    term.TermEnd,
            GradeEntryEnd:      term.TermEnd.AddDays(14));

        await svc.UpsertAsync(term.Id, newDto);

        ctx.TermAcademicCalendars.Count(c => c.TermId == term.Id).Should().Be(1);
        var cal = ctx.TermAcademicCalendars.Single(c => c.TermId == term.Id);
        cal.RegistrationStart.Should().Be(DateTime.SpecifyKind(term.TermStart.AddDays(-15), DateTimeKind.Utc));
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenTermCancelled()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx, cancelled: true);
        var svc = CreateService(ctx);

        Func<Task> act = async () => await svc.UpsertAsync(term.Id, ValidDto(term));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*iptal*");
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenRegistrationEndBeforeStart()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        var dto = new UpsertAcademicCalendarDto(
            RegistrationStart:  term.TermStart.AddDays(5),   // start AFTER end → invalid
            RegistrationEnd:    term.TermStart.AddDays(1),
            AddDropStart:       term.TermStart,
            AddDropEnd:         term.TermStart.AddDays(14),
            WithdrawalDeadline: term.TermEnd.AddDays(-30),
            GradeEntryStart:    term.TermEnd,
            GradeEntryEnd:      term.TermEnd.AddDays(14));

        Func<Task> act = async () => await svc.UpsertAsync(term.Id, dto);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenWithdrawalDeadlineAfterTermEnd()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        var dto = new UpsertAcademicCalendarDto(
            RegistrationStart:  term.TermStart.AddDays(-10),
            RegistrationEnd:    term.TermStart,
            AddDropStart:       term.TermStart,
            AddDropEnd:         term.TermStart.AddDays(14),
            WithdrawalDeadline: term.TermEnd.AddDays(5),    // AFTER TermEnd → invalid
            GradeEntryStart:    term.TermEnd,
            GradeEntryEnd:      term.TermEnd.AddDays(14));

        Func<Task> act = async () => await svc.UpsertAsync(term.Id, dto);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdatePhaseOverrideAsync_SetsOverride()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));
        await svc.UpdatePhaseOverrideAsync(term.Id, new UpdateCalendarPhaseOverrideDto("Registration", true));

        var cal = ctx.TermAcademicCalendars.Single(c => c.TermId == term.Id);
        cal.RegistrationOverride.Should().Be(true);
    }

    [Fact]
    public async Task UpdatePhaseOverrideAsync_Throws_WhenTermCancelled()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));

        // Cancel the term
        term.IsCancelled = true;
        ctx.SaveChanges();

        var act = () => svc.UpdatePhaseOverrideAsync(term.Id, new UpdateCalendarPhaseOverrideDto("Registration", true));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdatePhaseOverrideAsync_Throws_ForInvalidPhase()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));

        var act = () => svc.UpdatePhaseOverrideAsync(term.Id, new UpdateCalendarPhaseOverrideDto("Bogus", true));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetStudentViewAsync_ReturnsCalendar_WithPublicEventsOnly()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));

        // Add one public and one non-public event
        ctx.TermCalendarEvents.Add(new TermCalendarEvent
        {
            TermId = term.Id,
            EventType = CalendarEventType.ClassesStart,
            Title = "Derslerin Başlangıcı",
            StartDate = term.TermStart,
            IsPublic = true,
            IsGenerated = false,
            BlocksAcademicEvents = false
        });
        ctx.TermCalendarEvents.Add(new TermCalendarEvent
        {
            TermId = term.Id,
            EventType = CalendarEventType.Other,
            Title = "Gizli Etkinlik",
            StartDate = term.TermStart.AddDays(1),
            IsPublic = false,
            IsGenerated = false,
            BlocksAcademicEvents = false
        });
        ctx.SaveChanges();

        var result = await svc.GetStudentViewAsync(term.Id);

        result.Should().NotBeNull();
        result!.Events.Should().HaveCount(1);
        result.Events[0].IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveStudentViewAsync_ReturnsNull_WhenNoActiveTerm()
    {
        using var ctx = CreateContext();
        // Term with far-future dates — will NOT be active for clock in the past
        var term = new AcademicTerm
        {
            Name = "2030-2031 Güz",
            ShortName = "30G",
            EnglishName = "Fall 2030",
            ShortEnglishName = "30F",
            Year = 2030,
            TermType = TermType.Fall,
            TermStart = new DateTime(2030, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            TermEnd   = new DateTime(2031, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            IsCancelled = false,
            CreatedAt = DateTime.UtcNow
        };
        ctx.AcademicTerms.Add(term);
        ctx.SaveChanges();

        // Clock set to year 2024 — term is in the future, not active
        var svc = CreateService(ctx, now: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await svc.GetActiveStudentViewAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListTermsAsync_ReturnsAllTermsWithCalendarFlag()
    {
        using var ctx = CreateContext();
        var term1 = SeedTerm(ctx);
        var term2 = new AcademicTerm
        {
            Name = "2025-2026 Bahar",
            ShortName = "2025B",
            EnglishName = "2025-2026 Spring",
            ShortEnglishName = "25S",
            Year = 2025,
            TermType = TermType.Spring,
            TermStart = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc),
            TermEnd   = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            IsCancelled = false,
            CreatedAt = DateTime.UtcNow
        };
        ctx.AcademicTerms.Add(term2);
        ctx.SaveChanges();

        var svc = CreateService(ctx);

        // Only term1 gets a calendar
        await svc.UpsertAsync(term1.Id, ValidDto(term1));

        var list = await svc.ListTermsAsync();

        list.Should().HaveCount(2);
        list.Single(t => t.TermId == term1.Id).HasCalendar.Should().BeTrue();
        list.Single(t => t.TermId == term2.Id).HasCalendar.Should().BeFalse();
    }
}
