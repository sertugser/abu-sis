using System;

namespace SIS.Application.Scheduling.Solver;

/// <summary>
/// Plan 8 — Soft constraint eşik değerleri ve per-encoder weight'leri tek noktada.
/// SolverRequestBuilder SystemConfig'ten okuyup doldurur. Penalty formülü:
/// penalty_term = (excess_amount) × Weight; toplanır objective'e eklenir.
/// </summary>
/// <remarks>
/// Default değer kaynakları farklı:
/// <list type="bullet">
///   <item>**Threshold defaults** (S1MinOcc, S2MaxDailyHours, S5LunchBreakStart, vs.):
///     v1 <c>SchedulingConstraintConfig.FromSystemConfig</c> paritesi (lines 92-111).</item>
///   <item>**Weight defaults** (S1Weight, S2Weight, vs.): v2 (Plan 8) için tasarlanmış
///     CP-SAT'a özgü değerler. v1'de weight'ler <c>ConstraintDefinition.Weight</c>
///     entity'sinden (default 5) gelir; Plan 8 admin-tunable per-encoder weight pattern
///     kullanır. Plan 9'da real DB tune edilince override edilebilir.</item>
/// </list>
/// </remarks>
public sealed record SoftThresholds
{
    // S1 Occupancy
    public int S1MinOccupancyPercent { get; init; } = 60;
    public int S1MaxOccupancyPercent { get; init; } = 85;
    public int S1Weight { get; init; } = 1;

    // S2 DailyLoadSoft
    // Plan 13 (2026-05-15, kullanıcı A seçeneği): V2 encoder V1 ile birebir clock-hour
    // hesabı yapar. Per-section clock-minute = (TheoryHours + LabHours) × AverageSlotDurationMinutes.
    // Cap = S2MaxDailyHours × 60 minute (integer model). Default 6 saat — V1 default ile uyumlu.
    public int S2MaxDailyHours { get; init; } = 6;
    public int S2Weight { get; init; } = 10;

    /// <summary>
    /// Plan 13 — V2 encoder clock-hour migration: section'ın TheoryHours + LabHours
    /// kredi sayısını gerçek dakikaya çevirmek için slot ortalaması. Production'da
    /// genellikle 50dk. SolverRequestBuilder slot grid'inden hesaplar.
    /// </summary>
    public int AverageSlotDurationMinutes { get; init; } = 50;

    // S3 SlotCongestion
    public int S3MaxSectionsPerSlot { get; init; } = 5;
    public int S3Weight { get; init; } = 5;

    // S4 CoEnrollment
    public int S4MinCoPairThreshold { get; init; } = 20;
    public int S4Weight { get; init; } = 1;

    // S5 LunchBreakSoft
    public TimeOnly S5LunchBreakStart { get; init; } = new(11, 40);
    public TimeOnly S5LunchBreakEnd { get; init; } = new(12, 20);
    public int S5Weight { get; init; } = 8;

    // S7 StudentGap
    public int S7MaxGapSlots { get; init; } = 3;
    public int S7Weight { get; init; } = 3;

    // S8 InstructorDaySpread
    public int S8Weight { get; init; } = 4;

    // S12 WeekdayBalance
    public int S12MaxImbalancePercent { get; init; } = 20;
    public int S12Weight { get; init; } = 2;

    // S20 AdjacentYear
    public int S20PenaltyPerPair { get; init; } = 5;
    public int S20Weight { get; init; } = 1;

    // DIST soft tier
    public int DistDiscouragedWeight { get; init; } = 50;
    public int DistStronglyDiscouragedWeight { get; init; } = 200;

    // Plan 10 Phase 3 — Hard→Soft toggle weights for previously hard-only rules.
    // Defaults are conservative ("light penalty"). Admin tunes per Schedule.<Code>.Weight
    // SystemConfig keys when toggling Category to "Tercihli".
    public int H4Weight { get; init; } = 5;     // ClassroomType (theory/lab type mismatch per assignment)
    public int H5Weight { get; init; } = 10;    // CurriculumConflict (cohort overlap)
    public int H8Weight { get; init; } = 8;     // SameCourseSection (same-course sections overlap)
    public int H10Weight { get; init; } = 5;    // FacultyClassroom (faculty boundary cross — STUB)
    public int H15Weight { get; init; } = 6;    // MaxConsecutiveHours (excess hours per window)
    public int H16Weight { get; init; } = 5;    // TheoryLabDaySplit (theory + lab on same day above threshold)
    public int HWLWeight { get; init; } = 8;    // WeeklyLoad (instructor weekly excess hours)
    public int HDOWeight { get; init; } = 7;    // NoDayOff (instructor with no day off)
}
