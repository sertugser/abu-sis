using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H5_CurriculumConflictRuleTests
{
    [Fact]
    public void CurriculumMode_SharedDeptSameSemester_BothCompulsory_Reports()
    {
        // 2026-05-20: H5 curriculum mode artık iki ders de zorunlu olmalı.
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2))
            with { IsCompulsory = true };
        var c2 = Candidate(id: 2, courseId: 200,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2))
            with { IsCompulsory = true };

        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().ContainSingle().Which.ConflictType.Should().Be("CurriculumConflict");
    }

    [Fact]
    public void CurriculumMode_SharedDeptSameSemester_BothElective_NoConflict()
    {
        // İki seçmeli ders aynı saatte → öğrenci ikisinden birini seçer, uyarı yok.
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));

        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void CurriculumMode_DifferentSemester_NoConflict()
    {
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200,
            semestersByDept: new Dictionary<int, int> { [10] = 5 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));

        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void CurriculumMode_DifferentDept_NoConflict()
    {
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200,
            semestersByDept: new Dictionary<int, int> { [20] = 3 },
            accessibleDepts: new[] { 20 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));

        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void CurriculumMode_SameCourseId_NoConflict()
    {
        // existing.CourseId == c.CourseId → skip (same course, farklı section'ları H8 yakalar)
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 100,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));

        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void CohortMode_PairHit_Reports()
    {
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c1, c2 },
            CohortConflictPairs = new HashSet<(int, int)> { (1, 2) },
        };
        var ctx = new RuleContext { Candidate = c1, Snapshot = snapshot, Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().ContainSingle().Which.ConflictType.Should().Be("CurriculumConflict");
    }

    [Fact]
    public void EnrollmentMode_PairHit_Reports()
    {
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c1, c2 },
            EnrollmentConflicts = new EnrollmentConflictMatrix
            {
                HardConflictPairs = new HashSet<(int, int)> { (1, 2) },
            },
        };
        var ctx = new RuleContext { Candidate = c1, Snapshot = snapshot, Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().ContainSingle().Which.ConflictType.Should().Be("CurriculumConflict");
    }

    [Fact]
    public void NoTimeOverlap_NoConflict()
    {
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 13, durationHours: 2));

        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    // ── HasAlternativePairing: N×M kombinasyon kontrolü ──
    // 2026-05-19: Modulo pair çakışıyor olsa bile başka section kombinasyonunda
    // boş bir eşleşme varsa uyarı yutulur. Beklenti: öğrenci için en az bir alternatif.

    [Fact]
    public void CurriculumMode_AlternativeSectionExists_NoConflict()
    {
        // 3 section CourseX + 2 section CourseY → 6 kombinasyon.
        // X-1 ↔ Y-1 çakışıyor (modulo pair) ama X-3 ↔ Y-1 farklı saatte → alternatif var → skip.
        var rule = new H5_CurriculumConflictRule();
        var depts = new Dictionary<int, int> { [10] = 3 };
        var access = new[] { 10 };

        var x1 = Candidate(id: 1, courseId: 100, semestersByDept: depts, accessibleDepts: access,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var x2 = Candidate(id: 2, courseId: 100, semestersByDept: depts, accessibleDepts: access,
            blocks: Block(day: 2, startHour: 9, durationHours: 2));
        var x3 = Candidate(id: 3, courseId: 100, semestersByDept: depts, accessibleDepts: access,
            blocks: Block(day: 3, startHour: 9, durationHours: 2));
        var y1 = Candidate(id: 4, courseId: 200, semestersByDept: depts, accessibleDepts: access,
            blocks: Block(day: 1, startHour: 9, durationHours: 2)); // X-1 ile çakışır
        var y2 = Candidate(id: 5, courseId: 200, semestersByDept: depts, accessibleDepts: access,
            blocks: Block(day: 2, startHour: 9, durationHours: 2)); // X-2 ile çakışır

        var snapshot = Snapshot(x1, x2, x3, y1, y2);
        var ctx = new RuleContext { Candidate = x1, Snapshot = snapshot, Config = DefaultConfig() };

        // X-1 ↔ Y-1 modulo pair çakışıyor, AMA X-3 ↔ Y-1 (veya Y-2) çakışmıyor → alternatif var.
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void CurriculumMode_AllCombinationsConflict_Reports()
    {
        // Tüm kombinasyonlar aynı saatte + iki ders de zorunlu → gerçek darboğaz → uyarı verilir.
        var rule = new H5_CurriculumConflictRule();
        var depts = new Dictionary<int, int> { [10] = 3 };
        var access = new[] { 10 };

        var x1 = Candidate(id: 1, courseId: 100, semestersByDept: depts, accessibleDepts: access,
            blocks: Block(day: 1, startHour: 9, durationHours: 2)) with { IsCompulsory = true };
        var x2 = Candidate(id: 2, courseId: 100, semestersByDept: depts, accessibleDepts: access,
            blocks: Block(day: 1, startHour: 9, durationHours: 2)) with { IsCompulsory = true };
        var y1 = Candidate(id: 3, courseId: 200, semestersByDept: depts, accessibleDepts: access,
            blocks: Block(day: 1, startHour: 9, durationHours: 2)) with { IsCompulsory = true };
        var y2 = Candidate(id: 4, courseId: 200, semestersByDept: depts, accessibleDepts: access,
            blocks: Block(day: 1, startHour: 9, durationHours: 2)) with { IsCompulsory = true };

        var snapshot = Snapshot(x1, x2, y1, y2);
        var ctx = new RuleContext { Candidate = x1, Snapshot = snapshot, Config = DefaultConfig() };

        // 4 kombinasyon var, hepsi aynı saatte → alternatif yok → uyarı.
        rule.Evaluate(ctx).Should().NotBeEmpty();
    }

    [Fact]
    public void CohortMode_AlternativeSectionExists_NoConflict()
    {
        var rule = new H5_CurriculumConflictRule();
        var x1 = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var x2 = Candidate(id: 2, courseId: 100, blocks: Block(day: 3, startHour: 9, durationHours: 2));
        var y1 = Candidate(id: 3, courseId: 200, blocks: Block(day: 1, startHour: 9, durationHours: 2)); // X-1 ile çakışır
        var y2 = Candidate(id: 4, courseId: 200, blocks: Block(day: 2, startHour: 9, durationHours: 2)); // X-2 ile çakışmaz

        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { x1, x2, y1, y2 },
            // CohortBuilder modulo: (1,3), (2,4)
            CohortConflictPairs = new HashSet<(int, int)> { (1, 3), (2, 4) },
        };
        var ctx = new RuleContext { Candidate = x1, Snapshot = snapshot, Config = DefaultConfig() };

        // X-1 ↔ Y-1 modulo çakışıyor, AMA X-2 ↔ Y-2 farklı gün → alternatif var → skip.
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
