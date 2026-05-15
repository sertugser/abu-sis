using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class HWL_WeeklyLoadRuleTests
{
    [Fact]
    public void OverridePositive_ExceedingMax_Reports()
    {
        // override = 5h/week. existing 4h + candidate 3h = 7h > 5
        var rule = new HWL_WeeklyLoadRule();
        var existing = Candidate(id: 1, courseId: 100, instructorId: 99, instructorMaxWeeklyHours: 5,
            blocks: Block(day: 1, startHour: 9, durationHours: 4));
        var c = Candidate(id: 2, courseId: 200, instructorId: 99, instructorMaxWeeklyHours: 5,
            blocks: Block(day: 2, startHour: 9, durationHours: 3));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c, existing), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().ContainSingle().Which.ConflictType.Should().Be("WeeklyLoadConflict");
    }

    [Fact]
    public void OverridePositive_WithinMax_NoConflict()
    {
        var rule = new HWL_WeeklyLoadRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 99, instructorMaxWeeklyHours: 10,
            blocks: Block(day: 1, startHour: 9, durationHours: 3));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void OverrideZero_Skips()
    {
        // Plan 11 R10 not: predicate'in şu anki davranışı V1 paritesi — override=0 ise skip.
        // Faz 4'te DefaultInstructorMaxWeeklyHours config'e eklenince burada da enforce olacak.
        // Yığınlı saat: override 0 olduğu için bloklar evaluator'a hiç ulaşmıyor — yine de
        // 14 saatlik tek pencere haftalık limiti rahatlıkla aşardı.
        var rule = new HWL_WeeklyLoadRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 99, instructorMaxWeeklyHours: 0,
            blocks: Block(day: 1, startHour: 8, durationHours: 14));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void NoInstructor_Skips()
    {
        var rule = new HWL_WeeklyLoadRule();
        var c = Candidate(id: 1, courseId: 100,
            blocks: Block(day: 1, startHour: 9, durationHours: 3));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void ParallelSplitSameWindow_CountedOnce()
    {
        // Aynı (Day, Start, End) iki blok (farklı oda) tek pencere olarak sayılır.
        // 3h ≤ override 5h → no violation.
        var rule = new HWL_WeeklyLoadRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 99, instructorMaxWeeklyHours: 5,
            theory: 0, lab: 3, blocks: new[]
        {
            Block(day: 1, startHour: 9, durationHours: 3, classroomId: 5),
            Block(day: 1, startHour: 9, durationHours: 3, classroomId: 6),
        });
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
