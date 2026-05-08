using System;

namespace SIS.Application.Scheduling.Solver;

/// <summary>
/// Plan 8 — Soft constraint eşik değerleri ve per-encoder weight'leri tek noktada.
/// SolverRequestBuilder SystemConfig'ten okuyup doldurur. Default'lar v1
/// SchedulingConstraintConfig.FromSystemConfig paritesi (lines 92-111).
/// Penalty formülü: penalty_term = (excess_amount) × Weight; toplanır objective'e eklenir.
/// </summary>
public sealed record SoftThresholds
{
    // S1 Occupancy
    public int S1MinOccupancyPercent { get; init; } = 60;
    public int S1MaxOccupancyPercent { get; init; } = 85;
    public int S1Weight { get; init; } = 1;

    // S2 DailyLoadSoft
    public int S2MaxDailyHours { get; init; } = 6;
    public int S2Weight { get; init; } = 10;

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

    // S9 EarlyLate
    public TimeOnly S9MorningStart { get; init; } = new(8, 0);
    public TimeOnly S9EveningStart { get; init; } = new(17, 0);
    public int S9Weight { get; init; } = 6;

    // S12 WeekdayBalance
    public int S12MaxImbalancePercent { get; init; } = 20;
    public int S12Weight { get; init; } = 2;

    // S20 AdjacentYear
    public int S20PenaltyPerPair { get; init; } = 5;
    public int S20Weight { get; init; } = 1;

    // DIST soft tier
    public int DistDiscouragedWeight { get; init; } = 50;
    public int DistStronglyDiscouragedWeight { get; init; } = 200;
}
