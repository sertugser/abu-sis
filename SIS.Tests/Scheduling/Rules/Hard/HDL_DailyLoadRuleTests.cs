using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class HDL_DailyLoadRuleTests
{
    [Fact]
    public void InstructorDailyHoursExceedMax_Reports()
    {
        // MaxDailyHours = 4. existing 4h + candidate 2h aynı gün = 6h > 4
        var rule = new HDL_DailyLoadRule();
        var existing = Candidate(id: 1, courseId: 100, instructorId: 99,
            blocks: Block(day: 1, startHour: 9, durationHours: 4));
        var c = Candidate(id: 2, courseId: 200, instructorId: 99,
            blocks: Block(day: 1, startHour: 14, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c, existing),
            Config = DefaultConfig(cfg => cfg.MaxDailyHours = 4) };
        rule.Evaluate(ctx).Should().ContainSingle().Which.ConflictType.Should().Be("DailyLoadConflict");
    }

    [Fact]
    public void WithinLimit_NoConflict()
    {
        var rule = new HDL_DailyLoadRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 99,
            blocks: Block(day: 1, startHour: 9, durationHours: 3));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.MaxDailyHours = 4) };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void NoInstructor_Skips()
    {
        var rule = new HDL_DailyLoadRule();
        var c = Candidate(id: 1, courseId: 100,
            blocks: Block(day: 1, startHour: 9, durationHours: 9));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.MaxDailyHours = 4) };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void ParallelSplitSameWindow_CountedOnce()
    {
        // Aynı section'da paralel-lab: aynı (Day, Start, End) iki blok (farklı oda).
        // GroupBy (Start, End) ile tek sayım → 3h ≤ 4h max → no conflict.
        var rule = new HDL_DailyLoadRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 99, theory: 0, lab: 3, blocks: new[]
        {
            Block(day: 1, startHour: 9, durationHours: 3, classroomId: 5),
            Block(day: 1, startHour: 9, durationHours: 3, classroomId: 6),
        });
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.MaxDailyHours = 4) };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void DifferentDay_NoConflict()
    {
        // Aynı hoca farklı günlerde 4 + 3h — günlük max sınırı per-day, 4 max
        var rule = new HDL_DailyLoadRule();
        var existing = Candidate(id: 1, courseId: 100, instructorId: 99,
            blocks: Block(day: 1, startHour: 9, durationHours: 4));
        var c = Candidate(id: 2, courseId: 200, instructorId: 99,
            blocks: Block(day: 2, startHour: 9, durationHours: 3));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c, existing),
            Config = DefaultConfig(cfg => cfg.MaxDailyHours = 4) };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
