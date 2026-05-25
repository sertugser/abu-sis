using FluentAssertions;
using SIS.Application.Grading;
using SIS.Domain.Enums;

namespace SIS.Tests.Application;

/// <summary>
/// Unit tests for RelativeGradingService (bell-curve / bağıl değerlendirme).
///
/// ═══════════════════════════════════════════════════════════════════════
/// HAND-COMPUTED REFERENCE — Test 3 (T-score mapping)
/// ═══════════════════════════════════════════════════════════════════════
/// Population design: n=8 → {60, 60, 60, 60, 60, 60, 40, 80}
///   μ = (6×60 + 40 + 80) / 8 = 480 / 8 = 60
///   σ² = [6×(60-60)² + (40-60)² + (80-60)²] / 8
///      = [0 + 400 + 400] / 8 = 800 / 8 = 100
///   σ = 10
///
///   T = 50 + 10 × (score − 60) / 10 = 50 + (score − 60)
///     score=60 → T=50  → CB  (TCBMin=50)
///     score=80 → T=70  → AA  (TAAMin=70)
///     score=40 → T=30  → FF  (below TDDMin=35)
///
/// Thresholds used in all tests (unless overridden):
///   TAAMin=70, TBAMin=63, TBBMin=57, TCBMin=50, TCCMin=45, TDCMin=40, TDDMin=35
/// ═══════════════════════════════════════════════════════════════════════
/// </summary>
public class RelativeGradingServiceTests
{
    // ─── Shared helpers ────────────────────────────────────────────────────────

    /// <summary>Standard T-score thresholds used across most tests.</summary>
    private static RelativeGradeParams StandardParams(
        int minStudentCount = 2,
        decimal inclusionLimit = 0m,
        decimal absoluteFloor = 0m,
        GradeCode belowFloorGrade = GradeCode.FF) =>
        new(
            MinStudentCount: minStudentCount,
            InclusionLimit: inclusionLimit,
            AbsoluteFloor: absoluteFloor,
            TAAMin: 70, TBAMin: 63, TBBMin: 57,
            TCBMin: 50, TCCMin: 45, TDCMin: 40, TDDMin: 35,
            BelowFloorGrade: belowFloorGrade);

    /// <summary>
    /// Builds a 2-value symmetric population: n students at 50 + n students at 70.
    /// This population has μ=60, σ=10 (population standard deviation).
    ///
    /// Proof:
    ///   μ = (n×50 + n×70) / (2n) = 60
    ///   σ² = [n×(50-60)² + n×(70-60)²] / (2n) = (100+100)/2 = 100
    ///   σ = 10
    /// </summary>
    private static List<RelativeGradeInput> SymmetricDistribution(int n = 10, int idOffset = 0)
    {
        var inputs = new List<RelativeGradeInput>(2 * n);
        for (int i = 0; i < n; i++)
            inputs.Add(new RelativeGradeInput(idOffset + i, 50m));
        for (int i = n; i < 2 * n; i++)
            inputs.Add(new RelativeGradeInput(idOffset + i, 70m));
        return inputs;
    }

    /// <summary>
    /// Builds the canonical population for T-score boundary testing:
    /// {60×6, 40×1, 80×1}  (n=8)
    ///
    /// Proof:
    ///   μ = (6×60 + 40 + 80) / 8 = 480/8 = 60
    ///   σ² = [6×(60-60)² + (40-60)² + (80-60)²] / 8 = 800/8 = 100
    ///   σ = 10
    ///
    /// T-scores: score=60→50 (CB), score=80→70 (AA), score=40→30 (FF)
    /// </summary>
    private static List<RelativeGradeInput> MappingTestPopulation()
    {
        var inputs = new List<RelativeGradeInput>(8);
        // IDs 0-5: score 60 → T=50 → CB
        for (int i = 0; i < 6; i++)
            inputs.Add(new RelativeGradeInput(i, 60m));
        // ID 6: score 40 → T=30 → FF
        inputs.Add(new RelativeGradeInput(6, 40m));
        // ID 7: score 80 → T=70 → AA
        inputs.Add(new RelativeGradeInput(7, 80m));
        return inputs;
    }

    // ─── Test 1: Fallback when below minimum student count ─────────────────────

    [Fact]
    public void Evaluate_FallsBackToAbsolute_WhenBelowMinStudents()
    {
        // Arrange: only 2 students total, but MinStudentCount = 10
        var students = new List<RelativeGradeInput>
        {
            new(1, 55m),
            new(2, 65m)
        };
        var p = StandardParams(minStudentCount: 10);

        // Act
        var result = RelativeGradingService.Evaluate(students, p);

        // Assert
        result.FellBackToAbsolute.Should().BeTrue();
        result.Letters.Should().BeEmpty();
    }

    // ─── Test 2: Fallback when all scores are identical (σ = 0) ────────────────

    [Fact]
    public void Evaluate_FallsBackToAbsolute_WhenZeroStdDev()
    {
        // Arrange: 12 students all scoring 60 → σ = 0, bell-curve is meaningless
        var students = Enumerable.Range(1, 12)
            .Select(i => new RelativeGradeInput(i, 60m))
            .ToList();
        var p = StandardParams(minStudentCount: 10);

        // Act
        var result = RelativeGradingService.Evaluate(students, p);

        // Assert
        result.FellBackToAbsolute.Should().BeTrue();
        result.Letters.Should().BeEmpty();
    }

    // ─── Test 3: Correct T-score → letter mapping for known distribution ───────

    [Fact]
    public void Evaluate_MapsTScoresToLetters_ForKnownDistribution()
    {
        // Arrange
        // Population: {60×6, 40×1, 80×1}  →  μ=60, σ=10  (hand-verified above)
        //   ID 0-5: score=60 → T=50 → CB  (TCBMin=50)
        //   ID 6:   score=40 → T=30 → FF  (below TDDMin=35)
        //   ID 7:   score=80 → T=70 → AA  (TAAMin=70)
        var students = MappingTestPopulation();
        var p = StandardParams(minStudentCount: 2, inclusionLimit: 0m);

        // Act
        var result = RelativeGradingService.Evaluate(students, p);

        // Assert — no fallback
        result.FellBackToAbsolute.Should().BeFalse();

        // score=60 → T=50 → CB (boundary: T >= TCBMin=50)
        result.Letters[0].Should().Be(GradeCode.CB, because: "T=50 maps to CB (TCBMin=50)");
        result.Letters[3].Should().Be(GradeCode.CB, because: "all score-60 students get CB");

        // score=80 → T=70 → AA (boundary: T >= TAAMin=70)
        result.Letters[7].Should().Be(GradeCode.AA, because: "T=70 maps to AA (TAAMin=70)");

        // score=40 → T=30 → FF (below TDDMin=35)
        result.Letters[6].Should().Be(GradeCode.FF, because: "T=30 is below TDDMin=35 → FF (BelowFloorGrade)");

        // All 8 students have a grade
        result.Letters.Should().HaveCount(8);
    }

    // ─── Test 4: AbsoluteFloor overrides curve ─────────────────────────────────

    [Fact]
    public void Evaluate_AssignsBelowFloorGrade_WhenRawBelowAbsoluteFloor()
    {
        // Arrange
        // Use the mapping-test population (μ=60, σ=10):
        //   ID 6 has score=40, which is below AbsoluteFloor=41.
        //   Without the floor, this student would get FF from the T-score too,
        //   but we prove it's the floor mechanism by using BelowFloorGrade=DD (not FF).
        //
        //   For a student above the floor (e.g., ID 0 at score=60 → T=50 → CB):
        //   they must NOT receive BelowFloorGrade=DD.
        var students = MappingTestPopulation();
        var p = new RelativeGradeParams(
            MinStudentCount: 2,
            InclusionLimit: 0m,
            AbsoluteFloor: 41m,       // score=40 < 41 → BelowFloorGrade
            TAAMin: 70, TBAMin: 63, TBBMin: 57,
            TCBMin: 50, TCCMin: 45, TDCMin: 40, TDDMin: 35,
            BelowFloorGrade: GradeCode.DD);

        // Act
        var result = RelativeGradingService.Evaluate(students, p);

        // Assert
        result.FellBackToAbsolute.Should().BeFalse();

        // score=40 < AbsoluteFloor=41 → BelowFloorGrade=DD
        result.Letters[6].Should().Be(GradeCode.DD,
            because: "raw 40 < AbsoluteFloor 41 → BelowFloorGrade=DD");

        // score=60 ≥ AbsoluteFloor=41 → curve applies → T=50 → CB (not DD)
        result.Letters[0].Should().Be(GradeCode.CB,
            because: "raw 60 >= AbsoluteFloor 41 → curve gives CB, not floor grade");

        // score=80 ≥ AbsoluteFloor=41 → curve applies → T=70 → AA
        result.Letters[7].Should().Be(GradeCode.AA,
            because: "raw 80 >= AbsoluteFloor 41 → curve gives AA, not floor grade");
    }

    // ─── Test 5: InclusionLimit excludes outliers from stats ──────────────────

    [Fact]
    public void Evaluate_ExcludesBelowInclusionLimitFromStats_AndAssignsBelowFloor()
    {
        // Arrange
        // Symmetric distribution: 10@50 + 10@70 → μ=60, σ=10 (IDs 0-19)
        // Outlier (ID=999, score=10) is below InclusionLimit=30 → excluded from stats.
        // If the outlier were included, μ and σ would shift.
        // Proof that outlier is excluded: run the same curve with and without the
        // outlier; all included students must receive identical grades.
        var studentsWithoutOutlier = SymmetricDistribution(n: 10, idOffset: 0);
        var studentsWithOutlier    = SymmetricDistribution(n: 10, idOffset: 0);
        studentsWithOutlier.Add(new RelativeGradeInput(999, 10m));

        var p = new RelativeGradeParams(
            MinStudentCount: 2,
            InclusionLimit: 30m,
            AbsoluteFloor: 0m,
            TAAMin: 70, TBAMin: 63, TBBMin: 57,
            TCBMin: 50, TCCMin: 45, TDCMin: 40, TDDMin: 35,
            BelowFloorGrade: GradeCode.FF);

        // Act
        var resultWithout = RelativeGradingService.Evaluate(studentsWithoutOutlier, p);
        var resultWith    = RelativeGradingService.Evaluate(studentsWithOutlier, p);

        // Assert: neither run falls back
        resultWithout.FellBackToAbsolute.Should().BeFalse();
        resultWith.FellBackToAbsolute.Should().BeFalse();

        // Outlier must receive BelowFloorGrade
        resultWith.Letters[999].Should().Be(GradeCode.FF,
            because: "score 10 < InclusionLimit 30 → BelowFloorGrade");

        // All 20 included students must have IDENTICAL grades in both runs
        // (the outlier must not shift μ or σ)
        for (int i = 0; i < 20; i++)
        {
            resultWith.Letters[i].Should().Be(resultWithout.Letters[i],
                because: $"student {i} is included in both runs; outlier must not affect stats");
        }
    }

    // ─── Bonus: T=50 boundary exactly at CB ────────────────────────────────────

    [Fact]
    public void Evaluate_TScore50_MapsExactlyToCB()
    {
        // Population {60×6, 40, 80}: μ=60, σ=10 → a score-60 student gets T=50 → CB
        var students = MappingTestPopulation();
        var p = StandardParams(minStudentCount: 2);

        var result = RelativeGradingService.Evaluate(students, p);

        result.Letters[0].Should().Be(GradeCode.CB,
            because: "T=50 is exactly at TCBMin=50 boundary → CB");
    }

    // ─── Bonus: result dictionary covers ALL students (included + excluded) ─────

    [Fact]
    public void Evaluate_ReturnsLetterForEveryStudent()
    {
        // Symmetric distribution (IDs 0-19) + one excluded student (ID=500, score=5)
        var students = SymmetricDistribution(n: 10);
        students.Add(new RelativeGradeInput(500, 5m));

        var p = new RelativeGradeParams(
            MinStudentCount: 2,
            InclusionLimit: 30m,
            AbsoluteFloor: 0m,
            TAAMin: 70, TBAMin: 63, TBBMin: 57,
            TCBMin: 50, TCCMin: 45, TDCMin: 40, TDDMin: 35,
            BelowFloorGrade: GradeCode.FF);

        var result = RelativeGradingService.Evaluate(students, p);

        result.Letters.Should().ContainKey(500,
            because: "excluded students still receive a grade assignment");
        result.Letters.Count.Should().Be(students.Count,
            because: "every student in the input list must receive a grade");
    }
}
