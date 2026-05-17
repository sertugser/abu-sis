using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H1_ClassroomConflictRuleTests
{
    [Fact]
    public void TwoSectionsSameClassroomOverlap_Reports()
    {
        var rule = new H1_ClassroomConflictRule();
        var c1 = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var c2 = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };

        var violations = rule.Evaluate(ctx);
        violations.Should().ContainSingle().Which.ConflictType.Should().Be("ClassroomConflict");
    }

    [Fact]
    public void DifferentClassrooms_NoConflict()
    {
        var rule = new H1_ClassroomConflictRule();
        var c1 = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var c2 = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 6));
        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };

        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void ParallelLab_SameSectionTwoRooms_SelfSkip_NoConflict()
    {
        var rule = new H1_ClassroomConflictRule();
        // Aynı section iki paralel grup, aynı pencere, farklı odalar — self-skip aktif olmalı
        var c = Candidate(id: 1, courseId: 100, theory: 0, lab: 2, blocks: new[]
        {
            Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5),
            Block(day: 1, startHour: 9, durationHours: 2, classroomId: 6),
        });
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };

        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void NoBlocks_NoConflict()
    {
        var rule = new H1_ClassroomConflictRule();
        var c = Candidate(id: 1, courseId: 100);
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
