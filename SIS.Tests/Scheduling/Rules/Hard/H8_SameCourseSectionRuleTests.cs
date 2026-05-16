using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H8_SameCourseSectionRuleTests
{
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
    public void AllowSameCourseSectionOverlap_True_SkipsRule()
    {
        var rule = new H8_SameCourseSectionRule();
        var s1 = Candidate(id: 1, courseId: 100, theory: 2,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var s2 = Candidate(id: 2, courseId: 100, theory: 2,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = s1, Snapshot = Snapshot(s1, s2),
            Config = DefaultConfig(cfg => cfg.AllowSameCourseSectionOverlap = true) };
        rule.Evaluate(ctx).Should().BeEmpty();
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
}
