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
    /// Plan 11 Faz 1.2 — Hoca günlük max ders saati. V2 DailyLoadEncoder ile ortak kaynak.
    /// SystemConfig: <c>Schedule.MaxDailyHoursPerInstructor</c> (default 9).
    /// ConstraintParameterMapping üzerinden ConstraintDefinition.Parameters JSON
    /// <c>{"MaxDailyHours": ...}</c> ile per-constraint override edilebilir.
    /// </summary>
    public int MaxDailyHours { get; set; } = 9;

    /// <summary>
    /// Plan 11 Faz 1.4 — H4 Amphi rule flag. V2 ClassroomTypeEncoder ile parite.
    /// SystemConfig: <c>Schedule.AmphiThresholdEnabled</c> (default false).
    /// False iken Capacity ≥ AmphiThreshold zorunluluğu kaldırılır (V2'de zaten flag-gated).
    /// </summary>
    public bool AmphiThresholdEnabled { get; set; } = false;

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
    public bool S12Enabled { get; set; }
    public int S12MaxImbalancePercent { get; set; }

    /// <summary>
    /// S20 — Komşu Yıl Çakışma Soft Penalty.
    /// Aynı bölüm + |year_diff|=1 ders çiftlerinde modulo eşleşmiş sec_i↔sec_i pair
    /// çakışırsa pair başına bu kadar penalty uygulanır.
    /// </summary>
    public bool S20Enabled { get; set; }
    public double S20PenaltyPerPair { get; set; }

    public string CurriculumConflictMode { get; set; } = "curriculum";
    public int EnrollmentConflictHardThreshold { get; set; } = 1;

    /// <summary>
    /// SystemConfig key/value haritasından scheduling constraint config'i kurar.
    /// Hem manuel atama (SchedulingService) hem otomatik programlama (AutoScheduleEngine)
    /// hem simulasyon (SimulationService) tek yerden okusun diye burada toplu.
    ///
    /// Kısıda özel eşikler (LunchBreakStart/End, MaxConsecutiveHours, AmphiThreshold,
    /// TheoryLabSplitThreshold) artık ConstraintDefinition.Parameters JSON'undan geliyor —
    /// ConstraintEvaluationService.BuildEffectiveConfig per-evaluator override ediyor.
    /// Buradaki default'lar, kısıt kartında parametre yazılı değilse devreye girer.
    /// </summary>
    public static SchedulingConstraintConfig FromSystemConfig(IReadOnlyDictionary<string, string> config)
    {
        return new SchedulingConstraintConfig
        {
            // Hard — sadece global mod ayarları (kısıt kartında yer almayan)
            LunchBreakIsHard = GetBool(config, "Schedule.LunchBreak.IsHard", false),
            BreakDurationMinutes = GetInt(config, "Schedule.BreakDurationMinutes", 5),
            AllowSameCourseSectionOverlap = GetBool(config, "Schedule.AllowSameCourseSectionOverlap", false),
            SplittableBlockTypes = ParseSplittableBlockTypes(
                config.TryGetValue("Schedule.SplittableBlockTypes", out var sbt) ? sbt : "Lab"),

            // Default değerler (kısıt kartı Parameters JSON'u override eder)
            LunchBreakStart = new TimeOnly(11, 40),
            LunchBreakEnd = new TimeOnly(12, 20),
            MaxConsecutiveHours = 4,
            AmphiThreshold = 100,
            TheoryLabSplitThreshold = 4,

            // Plan 11 Faz 1.2 + 1.4 — V2 encoder ile parite kaynaklar
            MaxDailyHours = GetInt(config, "Schedule.MaxDailyHoursPerInstructor", 9),
            AmphiThresholdEnabled = GetBool(config, "Schedule.AmphiThresholdEnabled", false),

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
            S12Enabled = true,
            S12MaxImbalancePercent = GetInt(config, "SoftConstraint.S12.MaxImbalancePercent", 20),
            S20Enabled = true,
            S20PenaltyPerPair = GetDouble(config, "SoftConstraint.S20.PenaltyPerPair", 5.0),
            CurriculumConflictMode = config.TryGetValue("Schedule.CurriculumConflictMode", out var cm) ? cm : "curriculum",
            EnrollmentConflictHardThreshold = GetInt(config, "Schedule.EnrollmentConflictHardThreshold", 1),
        };
    }

    private static bool GetBool(IReadOnlyDictionary<string, string> config, string key, bool defaultValue) =>
        config.TryGetValue(key, out var v) && bool.TryParse(v, out var r) ? r : defaultValue;

    private static int GetInt(IReadOnlyDictionary<string, string> config, string key, int defaultValue) =>
        config.TryGetValue(key, out var v) && int.TryParse(v, out var r) ? r : defaultValue;

    private static double GetDouble(IReadOnlyDictionary<string, string> config, string key, double defaultValue) =>
        config.TryGetValue(key, out var v) && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : defaultValue;

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
