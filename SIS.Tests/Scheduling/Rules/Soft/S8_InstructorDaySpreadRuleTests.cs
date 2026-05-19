using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Soft;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Soft;

public sealed class S8_InstructorDaySpreadRuleTests
{
    [Fact]
    public void UsedDaysExceedMinPossible_Penalty()
    {
        // Hoca 3 gün × 2h = 6h. totalHours=6, S2MaxDailyHours=6 → minPossible=1.
        // usedDays=3, 3>1 → (3-1)*4 = 8
        // Snapshot exclude candidate → existing 2 day, candidate 1 day.
        var rule = new S8_InstructorDaySpreadRule();
        var existing1 = Candidate(id: 1, courseId: 100, instructorId: 1,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var existing2 = Candidate(id: 2, courseId: 200, instructorId: 1,
            blocks: Block(day: 2, startHour: 9, durationHours: 2));
        var c = Candidate(id: 3, courseId: 300, instructorId: 1,
            blocks: Block(day: 3, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(existing1, existing2),
            Config = DefaultConfig(cfg => { cfg.S8Enabled = true; cfg.S2MaxDailyHours = 6; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(8, 0.01);
    }

    [Fact]
    public void UsedDaysAtMinPossible_NoPenalty()
    {
        // 1 gün × 2h, minPossible=1, usedDays=1 → no penalty
        var rule = new S8_InstructorDaySpreadRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 1,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(),
            Config = DefaultConfig(cfg => { cfg.S8Enabled = true; cfg.S2MaxDailyHours = 6; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void NoInstructor_Skips()
    {
        var rule = new S8_InstructorDaySpreadRule();
        var c = Candidate(id: 1, courseId: 100,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => { cfg.S8Enabled = true; cfg.S2MaxDailyHours = 6; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void S8Disabled_NoPenalty()
    {
        var rule = new S8_InstructorDaySpreadRule();
        var existing = Candidate(id: 1, courseId: 100, instructorId: 1,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c = Candidate(id: 2, courseId: 200, instructorId: 1,
            blocks: Block(day: 3, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(existing),
            Config = DefaultConfig(cfg => cfg.S8Enabled = false) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }
}
