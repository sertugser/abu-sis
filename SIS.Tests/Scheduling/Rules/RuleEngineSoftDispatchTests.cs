using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules;

/// <summary>
/// Plan 12 Faz 4 — RuleEngine soft dispatch + breakdown integration testleri.
/// Hard dispatch Plan 11 testlerle korunuyor; bu suite soft path'i doğrular:
///   • CalculateCandidatePenaltyAsync weighted penalty toplamı döner
///   • EvaluateWithBreakdownAsync ScoreBreakdownDto kalemleri (Soft kategorisi + percentage)
///   • Plan 10 toggle Hard'a çekilirse Soft path skip
/// </summary>
public sealed class RuleEngineSoftDispatchTests
{
    [Fact]
    public async Task CalculateCandidatePenalty_S1Triggered_ReturnsWeightedPenalty()
    {
        // Capacity 10 / ClassroomCapacity 40 = %25 → S1 LowFill = (60-25)*0.5 = 17.5
        var engine = BuildEngine();
        var c = Candidate(id: 1, courseId: 100, capacity: 10, theory: 2,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 1, classroomCapacity: 40));
        var snapshot = Snapshot(c);
        var config = DefaultConfig(cfg =>
        {
            cfg.S1Enabled = true; cfg.S1MinOccupancy = 60; cfg.S1MaxOccupancy = 85;
        });

        var penalty = await engine.CalculateCandidatePenaltyAsync(c, snapshot, config, state: null, default);

        // State null → weight default 1 → 17.5 × 1 = 17.5
        penalty.Should().BeApproximately(17.5, 0.01);
    }

    [Fact]
    public async Task EvaluateWithBreakdown_AggregatesSoftPenalty_AcrossSections()
    {
        // 3 düşük doluluk section — her biri S1 LowFill tetikler. Snapshot.Sections döngüsü
        // her section'ı candidate yapar; toplam soft penalty = 3 × 17.5 = 52.5
        var engine = BuildEngine();
        var sections = Enumerable.Range(1, 3).Select(i => Candidate(
            id: i, courseId: 100 + i, capacity: 10, theory: 2,
            blocks: Block(day: i, startHour: 9, durationHours: 2, classroomId: i, classroomCapacity: 40))).ToArray();
        var snapshot = Snapshot(sections);
        var config = DefaultConfig(cfg =>
        {
            cfg.S1Enabled = true; cfg.S1MinOccupancy = 60; cfg.S1MaxOccupancy = 85;
        });

        var breakdown = await engine.EvaluateWithBreakdownAsync(snapshot, config, state: null, default);

        breakdown.TotalHardViolations.Should().Be(0);
        breakdown.Feasible.Should().BeTrue();
        breakdown.TotalSoftPenalty.Should().BeApproximately(52.5, 0.1);
        breakdown.Constraints.Should().Contain(item => item.Code == "S1" && item.Category == "Soft");

        var s1 = breakdown.Constraints.First(c => c.Code == "S1");
        s1.RawPenalty.Should().BeApproximately(52.5, 0.1);
        s1.AffectedSectionCount.Should().Be(3);
        s1.Percentage.Should().BeApproximately(100, 0.1); // tek soft kural → %100
    }

    [Fact]
    public async Task EvaluateWithBreakdown_NoTriggers_ReturnsFeasibleEmpty()
    {
        var engine = BuildEngine();
        var c = Candidate(id: 1, courseId: 100, capacity: 25, theory: 2,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 1, classroomCapacity: 40));
        var snapshot = Snapshot(c);

        var breakdown = await engine.EvaluateWithBreakdownAsync(snapshot, DefaultConfig(), state: null, default);

        breakdown.TotalHardViolations.Should().Be(0);
        breakdown.TotalSoftPenalty.Should().Be(0);
        breakdown.Feasible.Should().BeTrue();
    }

    [Fact]
    public async Task CalculateCandidatePenalty_NoSoftConstraintsEnabled_ReturnsZero()
    {
        var engine = BuildEngine();
        var c = Candidate(id: 1, courseId: 100, capacity: 10, theory: 2,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 1, classroomCapacity: 40));

        var penalty = await engine.CalculateCandidatePenaltyAsync(
            c, Snapshot(c), DefaultConfig(), state: null, default);

        // DefaultConfig'te S1Enabled=false → 0 penalty
        penalty.Should().Be(0);
    }
}
