using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H6_TheoryLabComponentRuleTests
{
    [Fact]
    public void CrossSection_LectureAndLabOverlap_Reports()
    {
        var rule = new H6_TheoryLabComponentRule();
        // Aynı CourseId: lecture-only + lab-only farklı section'lar
        var lecture = Candidate(id: 1, courseId: 100, theory: 2, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var lab = Candidate(id: 2, courseId: 100, theory: 0, lab: 2,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = lecture, Snapshot = Snapshot(lecture, lab), Config = DefaultConfig() };

        rule.Evaluate(ctx).Should().Contain(v => v.ConflictType == "SameCourseComponentConflict");
    }

    [Fact]
    public void DifferentCourse_NoConflict()
    {
        var rule = new H6_TheoryLabComponentRule();
        var lecture = Candidate(id: 1, courseId: 100, theory: 2, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var labOther = Candidate(id: 2, courseId: 200, theory: 0, lab: 2,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = lecture, Snapshot = Snapshot(lecture, labOther), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void MixedSection_BothComponents_NoCrossSectionViolation()
    {
        // Mixed section (T+L): cross-section rule skip
        var rule = new H6_TheoryLabComponentRule();
        var mixed = Candidate(id: 1, courseId: 100, theory: 2, lab: 2,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = mixed, Snapshot = Snapshot(mixed), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
