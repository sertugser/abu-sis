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
    public bool LunchBreakIsHard { get; set; }
    public TimeOnly LunchBreakStart { get; set; }
    public TimeOnly LunchBreakEnd { get; set; }
    public int MaxConsecutiveHours { get; set; }
    public int BreakDurationMinutes { get; set; }
    public int AmphiThreshold { get; set; }
    public int TheoryLabSplitThreshold { get; set; }
    public bool AllowSameCourseSectionOverlap { get; set; }

    // Soft constraint params
    public bool S1Enabled { get; set; }
    public int S1MinOccupancy { get; set; }
    public int S1MaxOccupancy { get; set; }
    public bool S2Enabled { get; set; }
    public int S2MaxDailyHours { get; set; }
    public bool S3Enabled { get; set; }
    public int S3MaxSectionsPerSlot { get; set; }
    public bool S4Enabled { get; set; }
    public int S4MinCoPairThreshold { get; set; }
    public bool S5Enabled { get; set; }
    public bool S7Enabled { get; set; }
    public int S7MaxGapSlots { get; set; }
    public bool S8Enabled { get; set; }
    public bool S9Enabled { get; set; }
    public TimeOnly S9MorningStart { get; set; }
    public TimeOnly S9EveningStart { get; set; }
    public bool S12Enabled { get; set; }
    public int S12MaxImbalancePercent { get; set; }
    public string CurriculumConflictMode { get; set; } = "curriculum";
    public int EnrollmentConflictHardThreshold { get; set; } = 1;
}
