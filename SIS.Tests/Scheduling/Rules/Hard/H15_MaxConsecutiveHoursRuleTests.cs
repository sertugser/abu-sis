using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H15_MaxConsecutiveHoursRuleTests
{
    [Fact]
    public void SingleBlockUnderMax_NoConflict()
    {
        var rule = new H15_MaxConsecutiveHoursRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 99,
            blocks: Block(day: 1, startHour: 9, durationHours: 3));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void ConsecutiveBlocksExceedMax_Reports()
    {
        // 9-12 (3h) + 12-14 (2h) — gap 0, total 5h > max 4h
        var rule = new H15_MaxConsecutiveHoursRule();
        var existing = Candidate(id: 1, courseId: 100, instructorId: 99,
            blocks: Block(day: 1, startHour: 9, durationHours: 3));
        var c = Candidate(id: 2, courseId: 200, instructorId: 99,
            blocks: Block(day: 1, startHour: 12, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c, existing),
            Config = DefaultConfig(cfg => cfg.MaxConsecutiveHours = 4) };
        rule.Evaluate(ctx).Should().ContainSingle().Which.ConflictType.Should().Be("MaxConsecutiveHoursConflict");
    }

    [Fact]
    public void GapLargerThanBreak_NoConflict()
    {
        // 9-11 + 13-15 — gap 120min > BreakDuration 5min → bloklar ayrı, kimse max'i aşmaz
        var rule = new H15_MaxConsecutiveHoursRule();
        var existing = Candidate(id: 1, courseId: 100, instructorId: 99,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c = Candidate(id: 2, courseId: 200, instructorId: 99,
            blocks: Block(day: 1, startHour: 13, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c, existing),
            Config = DefaultConfig(cfg => cfg.MaxConsecutiveHours = 4) };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void NoInstructor_Skips()
    {
        var rule = new H15_MaxConsecutiveHoursRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 5));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void NoBlocks_Skips()
    {
        var rule = new H15_MaxConsecutiveHoursRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 99);
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
