using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Soft;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Soft;

public sealed class S12_WeekdayBalanceRuleTests
{
    [Fact]
    public void HighImbalance_AboveThreshold_Penalty()
    {
        // 4 section Pzt, 1 section Cuma → counts [4, 1]. avg=2.5, diff=3.
        // threshold: 2.5 × 20/100 = 0.5. 3 > 0.5 → penalty = 3 × 2 = 6
        var rule = new S12_WeekdayBalanceRule();
        var monSections = Enumerable.Range(1, 3).Select(i => Candidate(
            id: i, courseId: 100 + i, instructorId: i,
            blocks: Block(day: 1, startHour: 9 + i * 2, durationHours: 2))).ToArray();
        var friSection = Candidate(id: 99, courseId: 999, instructorId: 99,
            blocks: Block(day: 5, startHour: 9, durationHours: 2));
        var c = Candidate(id: 100, courseId: 1000, instructorId: 100,
            blocks: Block(day: 1, startHour: 15, durationHours: 2));
        var ctx = new RuleContext
        {
            Candidate = c, Snapshot = Snapshot(monSections.Append(friSection).ToArray()),
            Config = DefaultConfig(cfg => { cfg.S12Enabled = true; cfg.S12MaxImbalancePercent = 20; }),
        };

        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(6, 0.01);
    }

    [Fact]
    public void BalancedDays_NoPenalty()
    {
        // 2 section Pzt, 2 section Cuma → counts [2,2]. avg=2, diff=0 → no penalty
        var rule = new S12_WeekdayBalanceRule();
        var sections = new[]
        {
            Candidate(id: 1, courseId: 100, instructorId: 1, blocks: Block(day: 1, startHour: 9, durationHours: 2)),
            Candidate(id: 2, courseId: 200, instructorId: 2, blocks: Block(day: 1, startHour: 13, durationHours: 2)),
            Candidate(id: 3, courseId: 300, instructorId: 3, blocks: Block(day: 5, startHour: 9, durationHours: 2)),
        };
        var c = Candidate(id: 4, courseId: 400, instructorId: 4,
            blocks: Block(day: 5, startHour: 13, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(sections),
            Config = DefaultConfig(cfg => { cfg.S12Enabled = true; cfg.S12MaxImbalancePercent = 20; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void SingleDay_Skips()
    {
        var rule = new S12_WeekdayBalanceRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 1,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(),
            Config = DefaultConfig(cfg => { cfg.S12Enabled = true; cfg.S12MaxImbalancePercent = 20; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void S12Disabled_NoPenalty()
    {
        var rule = new S12_WeekdayBalanceRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 1,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.S12Enabled = false) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }
}
