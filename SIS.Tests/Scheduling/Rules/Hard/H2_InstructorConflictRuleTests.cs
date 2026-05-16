using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H2_InstructorConflictRuleTests
{
    [Fact]
    public void SameInstructorOverlap_Reports()
    {
        var rule = new H2_InstructorConflictRule();
        var c1 = Candidate(id: 1, courseId: 100, instructorId: 7, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200, instructorId: 7, blocks: Block(day: 1, startHour: 10, durationHours: 2));
        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };

        var violations = rule.Evaluate(ctx);
        violations.Should().ContainSingle().Which.ConflictType.Should().Be("InstructorConflict");
    }

    [Fact]
    public void DifferentInstructors_NoConflict()
    {
        var rule = new H2_InstructorConflictRule();
        var c1 = Candidate(id: 1, courseId: 100, instructorId: 7, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200, instructorId: 8, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void NoInstructor_SkipsRule()
    {
        var rule = new H2_InstructorConflictRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: null, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void SameInstructorDifferentDays_NoConflict()
    {
        var rule = new H2_InstructorConflictRule();
        var c1 = Candidate(id: 1, courseId: 100, instructorId: 7, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200, instructorId: 7, blocks: Block(day: 2, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
