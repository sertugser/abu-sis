using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H13_LunchBreakRuleTests
{
    [Fact]
    public void OverlappingSlot_Reports()
    {
        var rule = new H13_LunchBreakRule();
        // Lunch 11:40-12:20; ders 12:00-13:00 (overlap)
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 12, durationHours: 1));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().Contain(v => v.ConflictType == "LunchBreakConflict");
    }

    [Fact]
    public void SlotBeforeLunch_NoConflict()
    {
        var rule = new H13_LunchBreakRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2)); // 9-11
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void SlotAfterLunch_NoConflict()
    {
        var rule = new H13_LunchBreakRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 13, durationHours: 2)); // 13-15
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
