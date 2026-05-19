using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Soft;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Soft;

public sealed class S20_AdjacentYearConflictRuleTests
{
    [Fact]
    public void AdjacentPairOverlap_Penalty()
    {
        // Pair (1,2), overlap block → S20PenaltyPerPair = 5
        var rule = new S20_AdjacentYearConflictRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var other = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 10, durationHours: 2));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1, Sections = new[] { c, other },
            AdjacentYearSoftPairs = new HashSet<(int, int)> { (1, 2) },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot,
            Config = DefaultConfig(cfg => { cfg.S20Enabled = true; cfg.S20PenaltyPerPair = 5.0; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(5, 0.01);
    }

    [Fact]
    public void NoOverlap_NoPenalty()
    {
        var rule = new S20_AdjacentYearConflictRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var other = Candidate(id: 2, courseId: 200, blocks: Block(day: 2, startHour: 9, durationHours: 2));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1, Sections = new[] { c, other },
            AdjacentYearSoftPairs = new HashSet<(int, int)> { (1, 2) },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot,
            Config = DefaultConfig(cfg => { cfg.S20Enabled = true; cfg.S20PenaltyPerPair = 5.0; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void NoPairsInSnapshot_NoPenalty()
    {
        var rule = new S20_AdjacentYearConflictRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => { cfg.S20Enabled = true; cfg.S20PenaltyPerPair = 5.0; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void S20Disabled_NoPenalty()
    {
        var rule = new S20_AdjacentYearConflictRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var other = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 10, durationHours: 2));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1, Sections = new[] { c, other },
            AdjacentYearSoftPairs = new HashSet<(int, int)> { (1, 2) },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot,
            Config = DefaultConfig(cfg => cfg.S20Enabled = false) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }
}
