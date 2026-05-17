using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Soft;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Soft;

public sealed class S2_DailyLoadSoftRuleTests
{
    [Fact]
    public void InstructorDailyHoursExceed_Penalty()
    {
        // existing 5h Monday + candidate 2h Monday, V1 quirk candidate'i çift sayar:
        // allSlots toplam Monday = 5 + 2 (existing) + 2 (candidate ayrı concat) = 9h
        // (9 - 6) * 10 = 30
        var rule = new S2_DailyLoadSoftRule();
        var existing = Candidate(id: 1, courseId: 100, instructorId: 1,
            blocks: Block(day: 1, startHour: 9, durationHours: 5));
        var c = Candidate(id: 2, courseId: 200, instructorId: 1,
            blocks: Block(day: 1, startHour: 15, durationHours: 2));
        var ctx = new RuleContext
        {
            Candidate = c, Snapshot = Snapshot(c, existing),
            Config = DefaultConfig(cfg => { cfg.S2Enabled = true; cfg.S2MaxDailyHours = 6; }),
        };

        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(30, 0.01);
    }

    [Fact]
    public void WithinLimit_NoPenalty()
    {
        var rule = new S2_DailyLoadSoftRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 1,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => { cfg.S2Enabled = true; cfg.S2MaxDailyHours = 6; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void NoInstructor_NoPenalty()
    {
        var rule = new S2_DailyLoadSoftRule();
        var c = Candidate(id: 1, courseId: 100,
            blocks: Block(day: 1, startHour: 9, durationHours: 9));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => { cfg.S2Enabled = true; cfg.S2MaxDailyHours = 6; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void S2Disabled_NoPenalty()
    {
        var rule = new S2_DailyLoadSoftRule();
        var existing = Candidate(id: 1, courseId: 100, instructorId: 1,
            blocks: Block(day: 1, startHour: 9, durationHours: 5));
        var c = Candidate(id: 2, courseId: 200, instructorId: 1,
            blocks: Block(day: 1, startHour: 15, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c, existing),
            Config = DefaultConfig(cfg => cfg.S2Enabled = false) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }
}
