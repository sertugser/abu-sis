using FluentAssertions;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Model;

public class SlotBlockValidatorTests
{
    private static IReadOnlyList<TimeSlotInput> Slots(params (DayOfWeek day, int hour, int minute, int durationMin)[] specs) =>
        specs.Select((s, i) => new TimeSlotInput
        {
            Id = i + 1,
            DayOfWeek = s.day,
            SlotOrder = (i % 8) + 1,
            StartTime = new TimeOnly(s.hour, s.minute),
            EndTime = new TimeOnly(s.hour, s.minute).Add(TimeSpan.FromMinutes(s.durationMin)),
            AllowedMode = TimeSlotAllowedMode.Both
        }).ToList();

    [Fact]
    public void ComputeValidBlockStarts_Duration1_AllSlotsValid()
    {
        var slots = Slots(
            (DayOfWeek.Monday, 9, 0, 50),
            (DayOfWeek.Monday, 10, 0, 50),
            (DayOfWeek.Tuesday, 9, 0, 50));

        var result = SlotBlockValidator.ComputeValidBlockStarts(slots, duration: 1);

        result.Should().BeEquivalentTo(new[] { 0, 1, 2 });
    }

    [Fact]
    public void ComputeValidBlockStarts_Duration2_RejectsCrossDayBoundary()
    {
        // Mon 9:00, Mon 10:00, Tue 9:00 — slot 1 + slot 2 cross day
        var slots = Slots(
            (DayOfWeek.Monday, 9, 0, 50),
            (DayOfWeek.Monday, 10, 0, 50),
            (DayOfWeek.Tuesday, 9, 0, 50));

        var result = SlotBlockValidator.ComputeValidBlockStarts(slots, duration: 2);

        result.Should().BeEquivalentTo(new[] { 0 },
            because: "only Mon-09:00→Mon-10:50 is valid; slot 1 + slot 2 cross day boundary");
    }

    [Fact]
    public void ComputeValidBlockStarts_Duration2_RejectsExcessGap()
    {
        // Mon 9:00-9:50, Mon 11:00-11:50 — gap 70min > 10min threshold
        var slots = Slots(
            (DayOfWeek.Monday, 9, 0, 50),
            (DayOfWeek.Monday, 11, 0, 50));

        var result = SlotBlockValidator.ComputeValidBlockStarts(slots, duration: 2);

        result.Should().BeEmpty(
            because: "gap 70min > 10min threshold; no valid 2-slot block");
    }

    [Fact]
    public void ComputeValidBlockStarts_Duration2_AcceptsExactly10MinGap()
    {
        // Mon 9:00-9:50, Mon 10:00-10:50 — gap exactly 10min (boundary)
        var slots = Slots(
            (DayOfWeek.Monday, 9, 0, 50),
            (DayOfWeek.Monday, 10, 0, 50));

        var result = SlotBlockValidator.ComputeValidBlockStarts(slots, duration: 2);

        result.Should().BeEquivalentTo(new[] { 0 },
            because: "gap 10min == threshold (≤10) is accepted");
    }

    [Fact]
    public void ComputeValidBlockStarts_Duration3_RejectsLunchGap()
    {
        // Mon 9:00, Mon 10:00, Mon 12:30 (after lunch — 100min gap from slot 1)
        var slots = Slots(
            (DayOfWeek.Monday, 9, 0, 50),
            (DayOfWeek.Monday, 10, 0, 50),
            (DayOfWeek.Monday, 12, 30, 50));

        var result = SlotBlockValidator.ComputeValidBlockStarts(slots, duration: 3);

        result.Should().BeEmpty(
            because: "100min gap between slot 1 and slot 2 (lunch) breaks the 3-slot block");
    }

    [Fact]
    public void ComputeValidBlockStarts_DurationExceedsSlotCount_ReturnsEmpty()
    {
        var slots = Slots(
            (DayOfWeek.Monday, 9, 0, 50),
            (DayOfWeek.Monday, 10, 0, 50));

        var result = SlotBlockValidator.ComputeValidBlockStarts(slots, duration: 3);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeValidBlockStarts_ZeroDuration_ReturnsEmpty()
    {
        var slots = Slots((DayOfWeek.Monday, 9, 0, 50));

        var result = SlotBlockValidator.ComputeValidBlockStarts(slots, duration: 0);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeValidBlockStarts_EmptyTimeSlots_ReturnsEmpty()
    {
        var result = SlotBlockValidator.ComputeValidBlockStarts(
            Array.Empty<TimeSlotInput>(), duration: 1);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeValidBlockStarts_FiveDayGridFourSlotsPerDay_3HourBlock()
    {
        // Mon-Fri, 4 slots/day. 3-saatlik blok: günde 2 valid başlangıç (slot 0, 1).
        // 5 gün × 2 valid start = 10 toplam.
        var slots = new List<TimeSlotInput>();
        for (int d = 1; d <= 5; d++)
            for (int s = 0; s < 4; s++)
                slots.Add(new TimeSlotInput
                {
                    Id = (d - 1) * 4 + s + 1,
                    DayOfWeek = (DayOfWeek)d,
                    SlotOrder = s + 1,
                    StartTime = new TimeOnly(9 + s, 0),
                    EndTime = new TimeOnly(9 + s, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                });

        var result = SlotBlockValidator.ComputeValidBlockStarts(slots, duration: 3);

        result.Should().HaveCount(10,
            because: "5 days × 2 valid starts/day (slot 0, 1) = 10");
        result.Should().BeEquivalentTo(new[] { 0, 1, 4, 5, 8, 9, 12, 13, 16, 17 });
    }
}
