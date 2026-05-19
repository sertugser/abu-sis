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

    // ── Kural 4: Course catalog override (RequiredTheoryClassroomType / RequiredLabClassroomType) ──
    // ClassroomTypeRequirementEnabled flag açık ve ders kataloğunda tip belirtilmişse, bu tip
    // dışındaki dersliklere yerleştirme ihlal sayılır (solver paritesi).

    [Fact]
    public void RequiredTheoryType_FlagEnabled_Mismatch_Reports()
    {
        var rule = new H4_ClassroomTypeRule();
        var c = Candidate(id: 1, courseId: 100, theory: 3, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 3, type: ClassroomType.Lecture))
            with { RequiredTheoryClassroomType = (int)ClassroomType.Amphitheater };
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.ClassroomTypeRequirementEnabled = true) };
        rule.Evaluate(ctx).Should().Contain(v => v.Message.Contains("uyuşmuyor"));
    }

    [Fact]
    public void RequiredTheoryType_FlagEnabled_Match_NoConflict()
    {
        var rule = new H4_ClassroomTypeRule();
        var c = Candidate(id: 1, courseId: 100, theory: 3, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 3, type: ClassroomType.Amphitheater))
            with { RequiredTheoryClassroomType = (int)ClassroomType.Amphitheater };
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.ClassroomTypeRequirementEnabled = true) };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void RequiredTheoryType_FlagDisabled_Mismatch_NoConflict()
    {
        // Flag kapalı → kural 4 atlanır, override ignore edilir (solver paritesi).
        var rule = new H4_ClassroomTypeRule();
        var c = Candidate(id: 1, courseId: 100, theory: 3, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 3, type: ClassroomType.Lecture))
            with { RequiredTheoryClassroomType = (int)ClassroomType.Amphitheater };
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig() }; // ClassroomTypeRequirementEnabled default false
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void RequiredLabType_FlagEnabled_Mismatch_Reports()
    {
        var rule = new H4_ClassroomTypeRule();
        var c = Candidate(id: 1, courseId: 100, theory: 0, lab: 2,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, type: ClassroomType.Lab))
            with { RequiredLabClassroomType = (int)ClassroomType.PhysicsLab };
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.ClassroomTypeRequirementEnabled = true) };
        // Lab section + RequiredLabClassroomType=PhysicsLab + atanan Lab → mismatch (PhysicsLab ≠ Lab).
        rule.Evaluate(ctx).Should().Contain(v => v.Message.Contains("uyuşmuyor"));
    }

    [Fact]
    public void RequiredTheoryType_TheoryInLab_AdminOverridesLabVeto_NoConflict()
    {
        // Admin kataloğda teori dersini bilerek Lab tipinde tanımlamış (override).
        // Flag açıkken kural 2 (lab kategori veto) atlanır, sadece override eşleşmesine bakılır.
        var rule = new H4_ClassroomTypeRule();
        var c = Candidate(id: 1, courseId: 100, theory: 3, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 3, type: ClassroomType.Lab))
            with { RequiredTheoryClassroomType = (int)ClassroomType.Lab };
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.ClassroomTypeRequirementEnabled = true) };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
