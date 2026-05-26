using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class HDO_NoDayOffRuleTests
{
    [Fact]
    public void InstructorUsesAllWorkdays_Reports()
    {
        // workdays = 5 (1..5 — other inst section'ları seed eder + candidate/existing). usedDays = 5 → violation.
        var rule = new HDO_NoDayOffRule();
        // Workday seed sections (different instructor) covering all 5 days
        var seed = Candidate(id: 99, courseId: 999, instructorId: 50, blocks: new[]
        {
            Block(day: 1, startHour: 9, durationHours: 1),
            Block(day: 2, startHour: 9, durationHours: 1),
            Block(day: 3, startHour: 9, durationHours: 1),
            Block(day: 4, startHour: 9, durationHours: 1),
            Block(day: 5, startHour: 9, durationHours: 1),
        });
        // Target instructor 99 covers all 5 days as well
        var existing = Candidate(id: 1, courseId: 100, instructorId: 99, blocks: new[]
        {
            Block(day: 2, startHour: 10, durationHours: 1),
            Block(day: 3, startHour: 10, durationHours: 1),
            Block(day: 4, startHour: 10, durationHours: 1),
            Block(day: 5, startHour: 10, durationHours: 1),
        });
        var c = Candidate(id: 2, courseId: 200, instructorId: 99,
            blocks: Block(day: 1, startHour: 10, durationHours: 1));

        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c, existing, seed), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().ContainSingle().Which.ConflictType.Should().Be("NoDayOffConflict");
    }

    [Fact]
    public void InstructorUsesFourOfFive_NoConflict()
    {
        // 5 workday seeded, instructor only uses 4 → maxAllowed = 4, usedDays = 4, no violation
        var rule = new HDO_NoDayOffRule();
        var seed = Candidate(id: 99, courseId: 999, instructorId: 50, blocks: new[]
        {
            Block(day: 1, startHour: 9, durationHours: 1),
            Block(day: 2, startHour: 9, durationHours: 1),
            Block(day: 3, startHour: 9, durationHours: 1),
            Block(day: 4, startHour: 9, durationHours: 1),
            Block(day: 5, startHour: 9, durationHours: 1),
        });
        var existing = Candidate(id: 1, courseId: 100, instructorId: 99, blocks: new[]
        {
            Block(day: 2, startHour: 10, durationHours: 1),
            Block(day: 3, startHour: 10, durationHours: 1),
            Block(day: 4, startHour: 10, durationHours: 1),
        });
        var c = Candidate(id: 2, courseId: 200, instructorId: 99,
            blocks: Block(day: 1, startHour: 10, durationHours: 1));

        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c, existing, seed), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void CalculatePenalty_WhenViolation_ReturnsWeightedPenalty()
    {
        // Soft modda (admin "Tercihli") evaluator artık penalty üretir. Eskiden HDO_NoDayOffRule
        // ISoftRulePredicate değildi → RuleEngine soft modda bu kuralı atlıyordu (hiç raporlamıyordu).
        var rule = new HDO_NoDayOffRule();
        var seed = Candidate(id: 99, courseId: 999, instructorId: 50, blocks: new[]
        {
            Block(day: 1, startHour: 9, durationHours: 1),
            Block(day: 2, startHour: 9, durationHours: 1),
            Block(day: 3, startHour: 9, durationHours: 1),
            Block(day: 4, startHour: 9, durationHours: 1),
            Block(day: 5, startHour: 9, durationHours: 1),
        });
        var existing = Candidate(id: 1, courseId: 100, instructorId: 99, blocks: new[]
        {
            Block(day: 2, startHour: 10, durationHours: 1),
            Block(day: 3, startHour: 10, durationHours: 1),
            Block(day: 4, startHour: 10, durationHours: 1),
            Block(day: 5, startHour: 10, durationHours: 1),
        });
        var c = Candidate(id: 2, courseId: 200, instructorId: 99,
            blocks: Block(day: 1, startHour: 10, durationHours: 1));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c, existing, seed), Config = DefaultConfig() };

        var result = rule.CalculatePenalty(ctx);
        result.ViolationCount.Should().Be(1);
        result.RawPenalty.Should().BeApproximately(7, 0.01); // 1 ihlal × HDOWeight default (7)
    }

    [Fact]
    public void CalculatePenalty_WhenNoViolation_ReturnsZero()
    {
        var rule = new HDO_NoDayOffRule();
        var c = Candidate(id: 1, courseId: 100,
            blocks: Block(day: 1, startHour: 9, durationHours: 1));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void NoInstructor_Skips()
    {
        var rule = new HDO_NoDayOffRule();
        var c = Candidate(id: 1, courseId: 100,
            blocks: Block(day: 1, startHour: 9, durationHours: 1));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void SingleWorkday_Skips()
    {
        // Workdays count ≤ 1 → rule no-ops (degenerate case)
        var rule = new HDO_NoDayOffRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 99,
            blocks: Block(day: 1, startHour: 9, durationHours: 1));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
