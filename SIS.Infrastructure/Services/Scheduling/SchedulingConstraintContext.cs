using SIS.Application.Scheduling.Constraints;
using SIS.Infrastructure.Services.Scheduling;

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

    /// <summary>
    /// Kapasite yetersiz olduğunda paralel oda split'i yapılabilen block tipleri.
    /// Varsayılan: sadece Lab. Config: "Schedule.SplittableBlockTypes".
    /// </summary>
    public HashSet<BlockType> SplittableBlockTypes { get; set; } = new() { BlockType.Lab };

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

    /// <summary>
    /// SystemConfig key/value haritasından scheduling constraint config'i kurar.
    /// Hem manuel atama (SchedulingService) hem otomatik programlama (AutoScheduleEngine)
    /// hem simulasyon (SimulationService) tek yerden okusun diye burada toplu.
    /// </summary>
    public static SchedulingConstraintConfig FromSystemConfig(IReadOnlyDictionary<string, string> config)
    {
        return new SchedulingConstraintConfig
        {
            // Hard
            LunchBreakIsHard = GetBool(config, "Schedule.LunchBreak.IsHard", false),
            LunchBreakStart = GetTime(config, "Schedule.LunchBreakStart", new TimeOnly(11, 40)),
            LunchBreakEnd = GetTime(config, "Schedule.LunchBreakEnd", new TimeOnly(12, 20)),
            MaxConsecutiveHours = GetInt(config, "Schedule.MaxConsecutiveHours", 4),
            BreakDurationMinutes = GetInt(config, "Schedule.BreakDurationMinutes", 5),
            AmphiThreshold = GetInt(config, "Classroom.AmphiThreshold", 100),
            TheoryLabSplitThreshold = GetInt(config, "Schedule.TheoryLabSplitThreshold", 4),
            AllowSameCourseSectionOverlap = GetBool(config, "Schedule.AllowSameCourseSectionOverlap", false),
            SplittableBlockTypes = ParseSplittableBlockTypes(
                config.TryGetValue("Schedule.SplittableBlockTypes", out var sbt) ? sbt : "Lab"),

            // Soft — aç/kapat tek otorite ConstraintDefinition.IsEnabled (state.IsDisabled).
            // Evaluator-içi defansif S*Enabled bayrakları daima true.
            S1Enabled = true,
            S1MinOccupancy = GetInt(config, "Classroom.OccupancyTarget.Min", 60),
            S1MaxOccupancy = GetInt(config, "Classroom.OccupancyTarget.Max", 85),
            S2Enabled = true,
            S2MaxDailyHours = GetInt(config, "SoftConstraint.S2.MaxDailyHours", 6),
            S3Enabled = true,
            S3MaxSectionsPerSlot = GetInt(config, "SoftConstraint.S3.MaxSectionsPerSlot", 5),
            S4Enabled = true,
            S4MinCoPairThreshold = GetInt(config, "SoftConstraint.S4.MinCoPairThreshold", 20),
            S5Enabled = true,
            S7Enabled = true,
            S7MaxGapSlots = GetInt(config, "SoftConstraint.S7.MaxGapSlots", 3),
            S8Enabled = true,
            S9Enabled = true,
            S9MorningStart = GetTime(config, "SoftConstraint.S9.MorningStart", new TimeOnly(8, 0)),
            S9EveningStart = GetTime(config, "SoftConstraint.S9.EveningStart", new TimeOnly(17, 0)),
            S12Enabled = true,
            S12MaxImbalancePercent = GetInt(config, "SoftConstraint.S12.MaxImbalancePercent", 20),
            CurriculumConflictMode = config.TryGetValue("Schedule.CurriculumConflictMode", out var cm) ? cm : "curriculum",
            EnrollmentConflictHardThreshold = GetInt(config, "Schedule.EnrollmentConflictHardThreshold", 1),
        };
    }

    private static bool GetBool(IReadOnlyDictionary<string, string> config, string key, bool defaultValue) =>
        config.TryGetValue(key, out var v) && bool.TryParse(v, out var r) ? r : defaultValue;

    private static int GetInt(IReadOnlyDictionary<string, string> config, string key, int defaultValue) =>
        config.TryGetValue(key, out var v) && int.TryParse(v, out var r) ? r : defaultValue;

    private static TimeOnly GetTime(IReadOnlyDictionary<string, string> config, string key, TimeOnly defaultValue) =>
        config.TryGetValue(key, out var v) && TimeOnly.TryParse(v, out var r) ? r : defaultValue;

    private static HashSet<BlockType> ParseSplittableBlockTypes(string value)
    {
        var result = new HashSet<BlockType>();
        if (string.IsNullOrWhiteSpace(value))
            return result;

        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<BlockType>(token, ignoreCase: true, out var bt))
                result.Add(bt);
        }
        return result;
    }
}
