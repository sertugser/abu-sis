using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H8_SameCourseSectionRuleTests
{
    // ── Hard mode (Evaluate) ──

    [Fact]
    public void SameCourseDifferentSectionsOverlap_Reports()
    {
        var rule = new H8_SameCourseSectionRule();
        var s1 = Candidate(id: 1, courseId: 100, theory: 2,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var s2 = Candidate(id: 2, courseId: 100, theory: 2,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = s1, Snapshot = Snapshot(s1, s2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().ContainSingle().Which.ConflictType.Should().Be("SameCourseSectionConflict");
    }

    [Fact]
    public void DifferentCourses_NoConflict()
    {
        var rule = new H8_SameCourseSectionRule();
        var s1 = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var s2 = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = s1, Snapshot = Snapshot(s1, s2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void NoOverlap_Hard_NoConflict()
    {
        var rule = new H8_SameCourseSectionRule();
        var s1 = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var s2 = Candidate(id: 2, courseId: 100, blocks: Block(day: 1, startHour: 13, durationHours: 2));
        var ctx = new RuleContext { Candidate = s1, Snapshot = Snapshot(s1, s2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    // ── Soft mode (CalculatePenalty) ──
    // RuleEngine ResolveCategory state.Definitions["H8"].Category="Soft" iken bu yolu çağırır.

    [Fact]
    public void Overlap_Soft_Penalty()
    {
        var rule = new H8_SameCourseSectionRule();
        var s1 = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var s2 = Candidate(id: 2, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = s1, Snapshot = Snapshot(s1, s2), Config = DefaultConfig() };

        var result = rule.CalculatePenalty(ctx);
        result.RawPenalty.Should().BeApproximately(8, 0.01);
        result.ViolationCount.Should().Be(1);
    }

    [Fact]
    public void NoOverlap_Soft_NoPenalty()
    {
        var rule = new H8_SameCourseSectionRule();
        var s1 = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var s2 = Candidate(id: 2, courseId: 100, blocks: Block(day: 1, startHour: 13, durationHours: 2));
        var ctx = new RuleContext { Candidate = s1, Snapshot = Snapshot(s1, s2), Config = DefaultConfig() };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void DifferentCourses_Soft_NoPenalty()
    {
        var rule = new H8_SameCourseSectionRule();
        var s1 = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var s2 = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = s1, Snapshot = Snapshot(s1, s2), Config = DefaultConfig() };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }
}
