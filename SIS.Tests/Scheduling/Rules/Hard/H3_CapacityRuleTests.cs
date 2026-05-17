using FluentAssertions;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H3_CapacityRuleTests
{
    private static RuleContext BuildCtx(
        int sectionCapacity,
        params (int Day, int Hour, int ClassroomCapacity)[] blocks)
    {
        var runtimeBlocks = blocks.Select(b => new RuntimeBlock(
            b.Day, new TimeOnly(b.Hour, 0), new TimeOnly(b.Hour + 1, 0),
            ClassroomId: 1, ClassroomName: "C", ClassroomType: ClassroomType.Lecture,
            ClassroomCapacity: b.ClassroomCapacity)).ToArray();

        var candidate = new RuntimeCandidate
        {
            SectionId = 1, CourseId = 1, CourseCode = "T1",
            Capacity = sectionCapacity,
            TheoryHours = 1, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace,
            Blocks = runtimeBlocks,
        };

        return new RuleContext
        {
            Candidate = candidate,
            Snapshot = new RuntimeSnapshot { TermId = 1, Sections = new[] { candidate } },
            Config = new SchedulingConstraintConfig(),
        };
    }

    [Fact]
    public void ClassroomCapacityLowerThanSection_Reports()
    {
        var rule = new H3_CapacityRule();
        var v = rule.Evaluate(BuildCtx(sectionCapacity: 50, (1, 9, 30)));

        v.Should().ContainSingle();
        v[0].Message.Should().Contain("30/50");
    }

    [Fact]
    public void ClassroomCapacityEnough_NoViolation()
    {
        var rule = new H3_CapacityRule();
        var v = rule.Evaluate(BuildCtx(sectionCapacity: 30, (1, 9, 30)));
        v.Should().BeEmpty();
    }

    [Fact]
    public void ParallelLabCombinedCapacity_NoViolation()
    {
        // 80 öğrenci için 2 paralel oda (40+40) = combined 80 ≥ 80 → OK.
        var rule = new H3_CapacityRule();
        var v = rule.Evaluate(BuildCtx(sectionCapacity: 80, (1, 9, 40), (1, 9, 40)));
        v.Should().BeEmpty();
    }

    [Fact]
    public void ParallelLabCombinedCapacityInsufficient_Reports()
    {
        // 80 öğrenci için 30+30 = 60 < 80 → violation.
        var rule = new H3_CapacityRule();
        var v = rule.Evaluate(BuildCtx(sectionCapacity: 80, (1, 9, 30), (1, 9, 30)));
        v.Should().ContainSingle();
        v[0].Message.Should().Contain("60/80");
    }

    [Fact]
    public void NoBlocks_NoViolation()
    {
        var rule = new H3_CapacityRule();
        var ctx = BuildCtx(sectionCapacity: 30);
        var v = rule.Evaluate(ctx);
        v.Should().BeEmpty();
    }
}
