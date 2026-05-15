using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Soft;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Soft;

public sealed class S4_CoEnrollmentRuleTests
{
    [Fact]
    public void OverlapWithPairedCourse_Penalty()
    {
        // Course 1 ↔ Course 2 co-enrollment pair, StudentCount=10. Aynı slotta çakışıyor → 10*0.5 = 5
        var rule = new S4_CoEnrollmentRule();
        var c = Candidate(id: 1, courseId: 1, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var other = Candidate(id: 2, courseId: 2, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1, Sections = new[] { c, other },
            CoEnrollmentPairs = new[] { new CoEnrollmentRuntimePair(1, 2, 10) },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot,
            Config = DefaultConfig(cfg => cfg.S4Enabled = true) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(5, 0.01);
    }

    [Fact]
    public void NoOverlap_NoPenalty()
    {
        var rule = new S4_CoEnrollmentRule();
        var c = Candidate(id: 1, courseId: 1, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var other = Candidate(id: 2, courseId: 2, blocks: Block(day: 2, startHour: 9, durationHours: 2));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1, Sections = new[] { c, other },
            CoEnrollmentPairs = new[] { new CoEnrollmentRuntimePair(1, 2, 10) },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot,
            Config = DefaultConfig(cfg => cfg.S4Enabled = true) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void NoPairsInSnapshot_NoPenalty()
    {
        var rule = new S4_CoEnrollmentRule();
        var c = Candidate(id: 1, courseId: 1, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.S4Enabled = true) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void S4Disabled_NoPenalty()
    {
        var rule = new S4_CoEnrollmentRule();
        var c = Candidate(id: 1, courseId: 1, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var other = Candidate(id: 2, courseId: 2, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1, Sections = new[] { c, other },
            CoEnrollmentPairs = new[] { new CoEnrollmentRuntimePair(1, 2, 10) },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot,
            Config = DefaultConfig(cfg => cfg.S4Enabled = false) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }
}
