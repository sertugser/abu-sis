using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Soft;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Soft;

public sealed class S5_LunchBreakSoftRuleTests
{
    [Fact]
    public void BlockOverlapsLunch_Penalty()
    {
        // Block 11:00-13:00 lunch 11:40-12:20 ile çakışır → 8 puan
        var rule = new S5_LunchBreakSoftRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 11, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => { cfg.S5Enabled = true; cfg.LunchBreakIsHard = false; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(8, 0.01);
    }

    [Fact]
    public void BlockBeforeLunch_NoPenalty()
    {
        var rule = new S5_LunchBreakSoftRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => { cfg.S5Enabled = true; cfg.LunchBreakIsHard = false; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void LunchHard_Skips()
    {
        // H13 hard mod aktif → S5 skip (V1 davranışı)
        var rule = new S5_LunchBreakSoftRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 11, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => { cfg.S5Enabled = true; cfg.LunchBreakIsHard = true; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void S5Disabled_NoPenalty()
    {
        var rule = new S5_LunchBreakSoftRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 11, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.S5Enabled = false) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }
}
