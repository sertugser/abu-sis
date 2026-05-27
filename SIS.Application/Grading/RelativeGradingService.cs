using SIS.Domain.Enums;

namespace SIS.Application.Grading;

/// <summary>
/// Input for one student in a relative (bell-curve) grading run.
/// </summary>
public sealed record RelativeGradeInput(int RegistrationId, decimal RawScore);

/// <summary>
/// Configuration parameters for a relative grading run.
/// </summary>
/// <param name="MinStudentCount">
///   Minimum number of included students required to run the curve.
///   If the included set is smaller, the engine falls back to absolute grading.
/// </param>
/// <param name="InclusionLimit">
///   Students with RawScore &lt; InclusionLimit are excluded from statistics
///   (mean / standard deviation) and automatically receive BelowFloorGrade.
/// </param>
/// <param name="AbsoluteFloor">
///   If &gt; 0, any student whose RawScore &lt; AbsoluteFloor receives
///   BelowFloorGrade regardless of their T-score.
/// </param>
/// <param name="TAAMin">Minimum T-score (inclusive) for AA grade.</param>
/// <param name="TBAMin">Minimum T-score (inclusive) for BA grade.</param>
/// <param name="TBBMin">Minimum T-score (inclusive) for BB grade.</param>
/// <param name="TCBMin">Minimum T-score (inclusive) for CB grade.</param>
/// <param name="TCCMin">Minimum T-score (inclusive) for CC grade.</param>
/// <param name="TDCMin">Minimum T-score (inclusive) for DC grade.</param>
/// <param name="TDDMin">Minimum T-score (inclusive) for DD grade.</param>
/// <param name="BelowFloorGrade">
///   Grade assigned when a student fails an absolute floor or is below the
///   inclusion limit — i.e., they did not meaningfully participate in the curve.
///   This does NOT apply to students who participated in the curve but ranked
///   below TDDMin; those students receive <see cref="GradeCode.FF"/> (the
///   natural bottom of the T-score table), regardless of this setting.
///   Typically FF or DD depending on exam regulation configuration.
/// </param>
public sealed record RelativeGradeParams(
    int MinStudentCount,
    decimal InclusionLimit,
    decimal AbsoluteFloor,
    int TAAMin, int TBAMin, int TBBMin, int TCBMin, int TCCMin, int TDCMin, int TDDMin,
    GradeCode BelowFloorGrade);

/// <summary>
/// Result of a relative grading evaluation.
/// </summary>
/// <param name="FellBackToAbsolute">
///   True when the curve could not be applied (too few students, or zero
///   standard deviation). In this case Letters is empty and the caller must
///   apply absolute grading instead.
/// </param>
/// <param name="Letters">
///   Maps every student's RegistrationId to their assigned GradeCode.
///   Contains ALL students (included + excluded) when FellBackToAbsolute is false.
///   Empty when FellBackToAbsolute is true.
/// </param>
public sealed record RelativeGradeOutcome(
    bool FellBackToAbsolute,
    IReadOnlyDictionary<int, GradeCode> Letters);

/// <summary>
/// Pure, stateless bell-curve (bağıl değerlendirme) grading engine.
///
/// No database, no EF Core, no HTTP — deterministic and unit-testable.
///
/// Algorithm:
/// 1. Compute the "included" set: students with RawScore >= InclusionLimit.
/// 2. If included.Count &lt; MinStudentCount → fallback (FellBackToAbsolute=true).
/// 3. Population mean μ = avg(included scores).
/// 4. Population stddev σ = sqrt( Σ(x−μ)² / n ), n = included.Count.
/// 5. If σ &lt; 0.0001 → fallback (all scores identical, curve is meaningless).
/// 6. For every student (included and non-included):
///    a. If AbsoluteFloor &gt; 0 and RawScore &lt; AbsoluteFloor → BelowFloorGrade.
///    b. If student is NOT in included set → BelowFloorGrade.
///    c. Else T = 50 + 10 * (RawScore − μ) / σ  (computed in double).
///       Map T to letter via thresholds (highest threshold wins first):
///       T>=TAAMin→AA, >=TBAMin→BA, >=TBBMin→BB, >=TCBMin→CB,
///       >=TCCMin→CC, >=TDCMin→DC, >=TDDMin→DD, else FF.
///       Note: BelowFloorGrade is used ONLY in (a) and (b), never in (c).
/// </summary>
public static class RelativeGradingService
{
    private const double ZeroSpreadThreshold = 0.0001;

    /// <summary>
    /// Runs the bell-curve grading algorithm over the supplied student list.
    /// </summary>
    /// <param name="students">All students to be graded (must not be null).</param>
    /// <param name="p">Grading parameters (must not be null).</param>
    /// <returns>
    /// A <see cref="RelativeGradeOutcome"/> with FellBackToAbsolute=true and an
    /// empty dictionary when the curve cannot be applied, or FellBackToAbsolute=false
    /// with a complete id→grade mapping for every student.
    /// </returns>
    public static RelativeGradeOutcome Evaluate(
        IReadOnlyList<RelativeGradeInput> students,
        RelativeGradeParams p)
    {
        // ── Step 1: Build the included set ───────────────────────────────────
        var included = students
            .Where(s => s.RawScore >= p.InclusionLimit)
            .ToList();

        // ── Step 2: Minimum student count check ──────────────────────────────
        if (included.Count < p.MinStudentCount)
            return Fallback();

        // ── Step 3: Population mean ──────────────────────────────────────────
        double n = included.Count;
        double mu = included.Average(s => (double)s.RawScore);

        // ── Step 4: Population standard deviation (÷ n, not n−1) ────────────
        double sumSqDiff = included.Sum(s =>
        {
            double diff = (double)s.RawScore - mu;
            return diff * diff;
        });
        double sigma = Math.Sqrt(sumSqDiff / n);

        // ── Step 5: Zero-spread check ─────────────────────────────────────────
        if (sigma < ZeroSpreadThreshold)
            return Fallback();

        // ── Step 6: Assign a letter to every student ──────────────────────────
        var includedIds = new HashSet<int>(included.Select(s => s.RegistrationId));
        var letters = new Dictionary<int, GradeCode>(students.Count);

        foreach (var student in students)
        {
            letters[student.RegistrationId] = AssignLetter(student, includedIds, mu, sigma, p);
        }

        return new RelativeGradeOutcome(FellBackToAbsolute: false, Letters: letters);
    }

    // ─── Private helpers ───────────────────────────────────────────────────────

    private static RelativeGradeOutcome Fallback() =>
        new(FellBackToAbsolute: true,
            Letters: new Dictionary<int, GradeCode>());

    private static GradeCode AssignLetter(
        RelativeGradeInput student,
        HashSet<int> includedIds,
        double mu,
        double sigma,
        RelativeGradeParams p)
    {
        // Absolute floor takes precedence over everything else.
        if (p.AbsoluteFloor > 0m && student.RawScore < p.AbsoluteFloor)
            return p.BelowFloorGrade;

        // Students below the inclusion limit are not curved.
        if (!includedIds.Contains(student.RegistrationId))
            return p.BelowFloorGrade;

        // Compute T-score in double for numeric stability.
        double t = 50.0 + 10.0 * ((double)student.RawScore - mu) / sigma;

        return MapTScoreToLetter(t, p);
    }

    private static GradeCode MapTScoreToLetter(double t, RelativeGradeParams p)
    {
        if (t >= p.TAAMin) return GradeCode.AA;
        if (t >= p.TBAMin) return GradeCode.BA;
        if (t >= p.TBBMin) return GradeCode.BB;
        if (t >= p.TCBMin) return GradeCode.CB;
        if (t >= p.TCCMin) return GradeCode.CC;
        if (t >= p.TDCMin) return GradeCode.DC;
        if (t >= p.TDDMin) return GradeCode.DD;
        // A student who participated in the curve (above all floors / inclusion
        // limits) but ranked below TDDMin gets FF — the natural bottom of the
        // T-score table. BelowFloorGrade is reserved for the two early-return
        // floor/inclusion cases in AssignLetter, not for the curve bottom.
        return GradeCode.FF;
    }
}
