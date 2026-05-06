using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class TimeSlotServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenDayIsNotConfiguredWorkDay_Throws()
    {
        await using var context = CreateContext();
        SeedEditableTerm(context);
        var service = CreateService(context, new Dictionary<string, string>
        {
            ["Schedule.WorkDays"] = "Monday,Tuesday,Wednesday,Thursday,Friday",
        });

        var act = () => service.CreateAsync(
            new CreateTimeSlotDto(1, DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(9, 50), TimeSlotAllowedMode.Both));

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*ders günü olarak tanımlı değil*");
    }

    [Fact]
    public async Task CreateAsync_WhenDurationDiffersFromConfiguredSlotDuration_Throws()
    {
        await using var context = CreateContext();
        SeedEditableTerm(context);
        var service = CreateService(context, new Dictionary<string, string>
        {
            ["Schedule.WorkDays"] = "Monday,Tuesday,Wednesday,Thursday,Friday",
            ["Schedule.SlotDurationMinutes"] = "60",
        });

        var act = () => service.CreateAsync(
            new CreateTimeSlotDto(1, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 50), TimeSlotAllowedMode.Both));

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*60 dakika*");
    }

    [Fact]
    public async Task CreateAsync_WhenLunchBreakIsHard_RejectsConfiguredOverlap()
    {
        await using var context = CreateContext();
        SeedEditableTerm(context);
        var service = CreateService(context, new Dictionary<string, string>
        {
            ["Schedule.WorkDays"] = "Monday,Tuesday,Wednesday,Thursday,Friday",
            ["Schedule.SlotDurationMinutes"] = "50",
            ["Schedule.LunchBreak.IsHard"] = "true",
            ["Schedule.LunchBreakStart"] = "11:40",
            ["Schedule.LunchBreakEnd"] = "12:20",
        });

        var act = () => service.CreateAsync(
            new CreateTimeSlotDto(1, DayOfWeek.Monday, new TimeOnly(11, 30), new TimeOnly(12, 20), TimeSlotAllowedMode.Both));

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*11:40-12:20*");
    }

    [Fact]
    public async Task CreateAsync_WhenLunchBreakWindowMoves_SameSlotCanBecomeValid()
    {
        await using var context = CreateContext();
        SeedEditableTerm(context);
        var service = CreateService(context, new Dictionary<string, string>
        {
            ["Schedule.WorkDays"] = "Monday,Tuesday,Wednesday,Thursday,Friday",
            ["Schedule.SlotDurationMinutes"] = "50",
            ["Schedule.LunchBreak.IsHard"] = "true",
            ["Schedule.LunchBreakStart"] = "12:30",
            ["Schedule.LunchBreakEnd"] = "13:10",
        });

        var slot = await service.CreateAsync(
            new CreateTimeSlotDto(1, DayOfWeek.Monday, new TimeOnly(11, 30), new TimeOnly(12, 20), TimeSlotAllowedMode.Both));

        slot.Should().NotBeNull();
        slot.StartTime.Should().Be(new TimeOnly(11, 30));
        slot.EndTime.Should().Be(new TimeOnly(12, 20));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static void SeedEditableTerm(AppDbContext context)
    {
        context.AcademicTerms.Add(new AcademicTerm
        {
            Id = 1,
            Name = "2025-2026 Bahar",
            ShortName = "2025-B",
            EnglishName = "2025-2026 Spring",
            ShortEnglishName = "2025 Spring",
            Year = 2025,
            TermType = TermType.Spring,
            TermStart = DateTime.UtcNow.AddDays(-10),
            TermEnd = DateTime.UtcNow.AddDays(90),
            IsCancelled = false,
            CreatedAt = DateTime.UtcNow,
        });

        context.SaveChanges();
    }

    private static TimeSlotService CreateService(AppDbContext context, IReadOnlyDictionary<string, string> values)
        => new(context, new FakeSystemConfigProvider(values));

    private sealed class FakeSystemConfigProvider(IReadOnlyDictionary<string, string> values) : ISystemConfigProvider
    {
        public Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
            => Task.FromResult(values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback);

        public Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
            => Task.FromResult(values.TryGetValue(key, out var value) && decimal.TryParse(value, out var parsed) ? parsed : fallback);

        public Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default)
            => Task.FromResult(values.TryGetValue(key, out var value) ? value : fallback);

        public Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default)
            => Task.FromResult(values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback);

        public void InvalidateCache(string key)
        {
        }
    }
}
