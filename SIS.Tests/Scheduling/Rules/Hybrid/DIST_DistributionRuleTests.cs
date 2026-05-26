using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.Rules.Hybrid;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hybrid;

public sealed class DIST_DistributionRuleTests
{
    private static RuntimeSnapshot SnapshotWithDist(
        IReadOnlyList<RuntimeCandidate> sections,
        DistributionType type,
        PreferenceLevel level,
        params (int CourseId, int? SectionId, int SortOrder)[] courses)
    {
        var distCourses = courses
            .Select(t => new DistributionConstraintCourseInfo(t.CourseId, t.SectionId, t.SortOrder))
            .ToList();
        return new RuntimeSnapshot
        {
            TermId = 1, Sections = sections,
            DistributionConstraints = new[] { new RuntimeDistributionConstraint(type, level, distCourses) },
        };
    }

    [Fact]
    public void SameDay_Violated_Unavailable_HardConflict()
    {
        // SameDay requires both courses on same day; candidate Mon, other Tue → violation
        // PreferenceLevel.Unavailable → hard
        var rule = new DIST_DistributionRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var other = Candidate(id: 2, courseId: 200, blocks: Block(day: 2, startHour: 9, durationHours: 2));
        var snapshot = SnapshotWithDist(new[] { c, other },
            DistributionType.SameDay, PreferenceLevel.Unavailable,
            (100, null, 0), (200, null, 1));
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };

        var hard = rule.Evaluate(ctx);
        hard.Should().ContainSingle().Which.ConflictType.Should().Be("DistributionConstraint");

        // Soft penalty path skip eder (Unavailable hard'a düşer)
        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void DifferentDay_Violated_StronglyDiscouraged_UsesConfigWeight()
    {
        // DifferentDay requires distinct days; both Mon → violation.
        // Ceza ağırlığı config'ten gelir (DistStronglyDiscouragedWeight default 200) — encoder ile parite.
        var rule = new DIST_DistributionRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var other = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 14, durationHours: 2));
        var snapshot = SnapshotWithDist(new[] { c, other },
            DistributionType.DifferentDay, PreferenceLevel.StronglyDiscouraged,
            (100, null, 0), (200, null, 1));
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };

        rule.Evaluate(ctx).Should().BeEmpty();
        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(200, 0.01);
    }

    [Fact]
    public void Discouraged_UsesConfigWeight()
    {
        // Discouraged → DistDiscouragedWeight (default 50).
        var rule = new DIST_DistributionRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var other = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 14, durationHours: 2));
        var snapshot = SnapshotWithDist(new[] { c, other },
            DistributionType.DifferentDay, PreferenceLevel.Discouraged,
            (100, null, 0), (200, null, 1));
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };

        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(50, 0.01);
    }

    [Fact]
    public void Preferred_NotPenalized()
    {
        // Preferred/StronglyPreferred solver encoder'da modellenmiyor (skip); evaluator da
        // cezalandırmaz (eski "_ => 5.0" drift'i kaldırıldı).
        var rule = new DIST_DistributionRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var other = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 14, durationHours: 2));
        var snapshot = SnapshotWithDist(new[] { c, other },
            DistributionType.DifferentDay, PreferenceLevel.Preferred,
            (100, null, 0), (200, null, 1));
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void NoViolation_NoPenalty()
    {
        // DifferentDay satisfied (Mon vs Tue) → no violation
        var rule = new DIST_DistributionRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var other = Candidate(id: 2, courseId: 200, blocks: Block(day: 2, startHour: 9, durationHours: 2));
        var snapshot = SnapshotWithDist(new[] { c, other },
            DistributionType.DifferentDay, PreferenceLevel.StronglyDiscouraged,
            (100, null, 0), (200, null, 1));
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };

        rule.Evaluate(ctx).Should().BeEmpty();
        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void NoDistributionConstraints_NoPenalty()
    {
        var rule = new DIST_DistributionRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };

        rule.Evaluate(ctx).Should().BeEmpty();
        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void BackToBack_NotAdjacent_SoftViolation()
    {
        // BackToBack: gap ≤ 15min beklenir; 9-11 ve 14-16 → gap 3h → violated
        var rule = new DIST_DistributionRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var other = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 14, durationHours: 2));
        var snapshot = SnapshotWithDist(new[] { c, other },
            DistributionType.BackToBack, PreferenceLevel.StronglyDiscouraged,
            (100, null, 0), (200, null, 1));
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };

        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(200, 0.01);
    }

    [Fact]
    public void CandidateCourseNotInConstraint_NoEffect()
    {
        // DIST courses listesinde candidate'in CourseId yok → skip
        var rule = new DIST_DistributionRule();
        var c = Candidate(id: 1, courseId: 999, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var other = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 14, durationHours: 2));
        var snapshot = SnapshotWithDist(new[] { c, other },
            DistributionType.DifferentDay, PreferenceLevel.Unavailable,
            (100, null, 0), (200, null, 1));
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };

        rule.Evaluate(ctx).Should().BeEmpty();
        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }
}
