using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Soft;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Soft;

public sealed class S1_OccupancyRuleTests
{
    [Fact]
    public void LowFill_BelowMin_Penalty()
    {
        // Capacity 10 / ClassroomCapacity 40 = %25 < %60 → (60-25)*0.5 = 17.5
        var rule = new S1_OccupancyRule();
        var c = Candidate(id: 1, courseId: 100, capacity: 10,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 1, classroomCapacity: 40));
        var ctx = new RuleContext
        {
            Candidate = c,
            Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg =>
            {
                cfg.S1Enabled = true;
                cfg.S1MinOccupancy = 60;
                cfg.S1MaxOccupancy = 85;
            }),
        };

        var result = rule.CalculatePenalty(ctx);
        result.RawPenalty.Should().BeApproximately(17.5, 0.01);
        result.ViolationCount.Should().Be(1);
    }

    [Fact]
    public void HighFill_AboveMax_Penalty()
    {
        // 40/40 = %100 > %85 → (100-85)*0.5 = 7.5
        var rule = new S1_OccupancyRule();
        var c = Candidate(id: 1, courseId: 100, capacity: 40,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 1, classroomCapacity: 40));
        var ctx = new RuleContext
        {
            Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => { cfg.S1Enabled = true; cfg.S1MinOccupancy = 60; cfg.S1MaxOccupancy = 85; }),
        };

        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(7.5, 0.01);
    }

    [Fact]
    public void SweetSpot_NoPenalty()
    {
        // 25/40 = %62.5 ∈ [60, 85] → 0
        var rule = new S1_OccupancyRule();
        var c = Candidate(id: 1, courseId: 100, capacity: 25,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 1, classroomCapacity: 40));
        var ctx = new RuleContext
        {
            Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => { cfg.S1Enabled = true; cfg.S1MinOccupancy = 60; cfg.S1MaxOccupancy = 85; }),
        };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void S1Disabled_NoPenalty()
    {
        var rule = new S1_OccupancyRule();
        var c = Candidate(id: 1, courseId: 100, capacity: 5,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 1, classroomCapacity: 40));
        var ctx = new RuleContext
        {
            Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.S1Enabled = false),
        };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void NoBlocks_NoPenalty()
    {
        var rule = new S1_OccupancyRule();
        var c = Candidate(id: 1, courseId: 100, capacity: 30);
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.S1Enabled = true) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }
}
