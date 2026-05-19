using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H16_TheoryLabDaySplitRuleTests
{
    [Fact]
    public void TotalAboveThreshold_SameDay_Reports()
    {
        // theory 3h + lab 3h = 6h > threshold 4h. Theory pencere day=1 9-12; lab day=1 13-16.
        var rule = new H16_TheoryLabDaySplitRule();
        var c = Candidate(id: 1, courseId: 100, theory: 3, lab: 3, blocks: new[]
        {
            Block(day: 1, startHour: 9, durationHours: 3),
            Block(day: 1, startHour: 13, durationHours: 3),
        });
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.TheoryLabSplitThreshold = 4) };
        rule.Evaluate(ctx).Should().ContainSingle().Which.ConflictType.Should().Be("TheoryLabDaySplitConflict");
    }

    [Fact]
    public void TotalAboveThreshold_DifferentDays_NoConflict()
    {
        var rule = new H16_TheoryLabDaySplitRule();
        var c = Candidate(id: 1, courseId: 100, theory: 3, lab: 3, blocks: new[]
        {
            Block(day: 1, startHour: 9, durationHours: 3),
            Block(day: 3, startHour: 13, durationHours: 3),
        });
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.TheoryLabSplitThreshold = 4) };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void TotalAtThreshold_SameDay_NoConflict()
    {
        // theory 2 + lab 2 = 4 == threshold 4 — kural sadece > eşik ise tetiklenir
        var rule = new H16_TheoryLabDaySplitRule();
        var c = Candidate(id: 1, courseId: 100, theory: 2, lab: 2, blocks: new[]
        {
            Block(day: 1, startHour: 9, durationHours: 2),
            Block(day: 1, startHour: 13, durationHours: 2),
        });
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.TheoryLabSplitThreshold = 4) };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void TheoryOnly_Skips()
    {
        var rule = new H16_TheoryLabDaySplitRule();
        var c = Candidate(id: 1, courseId: 100, theory: 5, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 5));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.TheoryLabSplitThreshold = 4) };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void LabOnly_Skips()
    {
        var rule = new H16_TheoryLabDaySplitRule();
        var c = Candidate(id: 1, courseId: 100, theory: 0, lab: 5,
            blocks: Block(day: 1, startHour: 9, durationHours: 5));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.TheoryLabSplitThreshold = 4) };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
