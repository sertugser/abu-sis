namespace SIS.Application.Scheduling.Constraints;

public sealed record SnapshotDto(
    int Id, int TermId, DateTime SnapshotAt, string SnapshotType,
    int TotalHardViolations, double TotalSoftPenalty, bool IsFeasible);

public sealed record SnapshotDetailDto(
    int Id, string ConstraintCode, string Category,
    int ViolationCount, double Penalty, double WeightedPenalty, int AffectedSectionCount);

public sealed record TrendDataDto(
    int TermId,
    IReadOnlyList<TrendPointDto> DataPoints,
    TrendSummaryDto Improvement);

public sealed record TrendPointDto(
    DateTime SnapshotAt, string SnapshotType,
    int HardViolations, double SoftPenalty, bool IsFeasible);

public sealed record TrendSummaryDto(
    string HardReduction, string PenaltyReduction, string Trend);

public sealed record SnapshotComparisonDto(
    SnapshotDto Snapshot1, SnapshotDto Snapshot2,
    IReadOnlyList<ComparisonItemDto> Items,
    int ImprovedCount, int WorsenedCount, int UnchangedCount);

public sealed record ComparisonItemDto(
    string ConstraintCode, string Category,
    int ViolationsBefore, int ViolationsAfter, int ViolationDelta,
    double PenaltyBefore, double PenaltyAfter, double PenaltyDelta,
    string Change);

public sealed record HeatmapDto(
    IReadOnlyList<HeatmapRowDto> Rows);

public sealed record HeatmapRowDto(
    string ConstraintCode, string ConstraintName,
    IReadOnlyList<HeatmapCellDto> Cells);

public sealed record HeatmapCellDto(
    int DayOfWeek, int SlotIndex, int ViolationCount, string Severity);

public sealed record BottleneckDto(
    IReadOnlyList<BottleneckConstraintDto> TopConstraints,
    IReadOnlyList<BottleneckResourceDto> TopResources,
    IReadOnlyList<BottleneckSlotDto> TopTimeSlots);

public sealed record BottleneckConstraintDto(
    string Code, string Name, double Percentage, int ViolationCount);

public sealed record BottleneckResourceDto(
    string Type, string Name, int ConflictCount);

public sealed record BottleneckSlotDto(
    string Day, string Slot, int TotalViolations);
