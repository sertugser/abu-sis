using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services;

public sealed class SchedulingConstraintContext : IConstraintContext
{
    public required SectionSnapshot Candidate { get; init; }
    public required TermSnapshot Snapshot { get; init; }
    public required SchedulingConstraintConfig Config { get; init; }
}

public sealed class SchedulingConstraintConfig
{
    // Hard constraint params
    public bool LunchBreakIsHard { get; init; }
    public TimeOnly LunchBreakStart { get; init; }
    public TimeOnly LunchBreakEnd { get; init; }
    public int MaxConsecutiveHours { get; init; }
    public int BreakDurationMinutes { get; init; }
    public int AmphiThreshold { get; init; }
    public int TheoryLabSplitThreshold { get; init; }
    public bool AllowSameCourseSectionOverlap { get; init; }

    // Soft constraint params
    public bool S1Enabled { get; init; }
    public int S1MinOccupancy { get; init; }
    public int S1MaxOccupancy { get; init; }
    public bool S2Enabled { get; init; }
    public int S2MaxDailyHours { get; init; }
    public bool S3Enabled { get; init; }
    public int S3MaxSectionsPerSlot { get; init; }
    public bool S4Enabled { get; init; }
    public int S4MinCoPairThreshold { get; init; }
    public bool S5Enabled { get; init; }
    public bool S7Enabled { get; init; }
    public int S7MaxGapSlots { get; init; }
    public bool S8Enabled { get; init; }
    public bool S9Enabled { get; init; }
    public TimeOnly S9MorningStart { get; init; }
    public TimeOnly S9EveningStart { get; init; }
    public bool S12Enabled { get; init; }
    public int S12MaxImbalancePercent { get; init; }
    public string CurriculumConflictMode { get; init; } = "curriculum";
    public int EnrollmentConflictHardThreshold { get; init; } = 1;
}
