using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H4_ClassroomTypeRuleTests
{
    [Fact]
    public void LabSection_InLectureClassroom_Reports()
    {
        var rule = new H4_ClassroomTypeRule();
        var c = Candidate(id: 1, courseId: 100, theory: 0, lab: 2,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, type: ClassroomType.Lecture));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };

        rule.Evaluate(ctx).Should().Contain(v => v.Message.Contains("Lab section"));
    }

    [Fact]
    public void LabSection_InComputerClassroom_NoConflict()
    {
        var rule = new H4_ClassroomTypeRule();
        var c = Candidate(id: 1, courseId: 100, theory: 0, lab: 2,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, type: ClassroomType.Computer));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void TheorySection_InLabClassroom_Reports()
    {
        var rule = new H4_ClassroomTypeRule();
        var c = Candidate(id: 1, courseId: 100, theory: 3, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 3, type: ClassroomType.Lab));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().Contain(v => v.Message.Contains("Teori dersi"));
    }

    [Fact]
    public void BigCapacity_AmphiFlagDisabled_NoConflict()
    {
        var rule = new H4_ClassroomTypeRule();
        var c = Candidate(id: 1, courseId: 100, capacity: 150, theory: 3, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 3, type: ClassroomType.Lecture));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => { cfg.AmphiThreshold = 100; cfg.AmphiThresholdEnabled = false; }) };
        rule.Evaluate(ctx).Should().NotContain(v => v.Message.Contains("Amfi"));
    }

    [Fact]
    public void BigCapacity_AmphiFlagEnabled_Reports()
    {
        var rule = new H4_ClassroomTypeRule();
        var c = Candidate(id: 1, courseId: 100, capacity: 150, theory: 3, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 3, type: ClassroomType.Lecture));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => { cfg.AmphiThreshold = 100; cfg.AmphiThresholdEnabled = true; }) };
        rule.Evaluate(ctx).Should().Contain(v => v.Message.Contains("Amfi"));
    }
}
