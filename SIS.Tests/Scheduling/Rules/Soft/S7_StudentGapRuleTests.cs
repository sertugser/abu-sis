using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Soft;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Soft;

public sealed class S7_StudentGapRuleTests
{
    private static RuntimeCandidate CohortCandidate(int id, int courseId, int? semester,
        params RuntimeBlock[] blocks)
    {
        var c = Candidate(id: id, courseId: courseId, departmentId: 1, blocks: blocks);
        return c with { SuggestedSemester = semester };
    }

    [Fact]
    public void AdjacentGapExceedsCap_Penalty()
    {
        // Same dept (1), same year (semester 3 → year 1), 9-11 ile 14-16 → gap 3h ≥ cap 2
        // (3 - 2 + 1) * 3 = 6
        var rule = new S7_StudentGapRule();
        var existing = CohortCandidate(1, 100, semester: 3,
            Block(day: 1, startHour: 9, durationHours: 2));
        var c = CohortCandidate(2, 200, semester: 3,
            Block(day: 1, startHour: 14, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(existing),
            Config = DefaultConfig(cfg => { cfg.S7Enabled = true; cfg.S7MaxGapSlots = 2; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(6, 0.01);
    }

    [Fact]
    public void GapBelowCap_NoPenalty()
    {
        var rule = new S7_StudentGapRule();
        var existing = CohortCandidate(1, 100, semester: 3,
            Block(day: 1, startHour: 9, durationHours: 2));
        var c = CohortCandidate(2, 200, semester: 3,
            Block(day: 1, startHour: 12, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(existing),
            Config = DefaultConfig(cfg => { cfg.S7Enabled = true; cfg.S7MaxGapSlots = 2; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void DifferentYear_NoPenalty()
    {
        // semester 3 (year 1) ile semester 5 (year 2) — farklı cohort
        var rule = new S7_StudentGapRule();
        var other = CohortCandidate(1, 100, semester: 5,
            Block(day: 1, startHour: 9, durationHours: 2));
        var c = CohortCandidate(2, 200, semester: 3,
            Block(day: 1, startHour: 14, durationHours: 2));
        // sameDeptYearSections boş (year farklı) → combined sadece c.Blocks → tek entry → no penalty
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(other),
            Config = DefaultConfig(cfg => { cfg.S7Enabled = true; cfg.S7MaxGapSlots = 2; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void NoDepartment_Skips()
    {
        var rule = new S7_StudentGapRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => { cfg.S7Enabled = true; cfg.S7MaxGapSlots = 2; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }
}
