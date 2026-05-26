using System.Text.Json;

namespace SIS.Application.Scheduling.Constraints;

/// <summary>
/// Plan 11 Faz 2 — SIS.Application'a taşındı (eski yer: SIS.Infrastructure/Services/Scheduling/
/// SchedulingConstraintContext.cs içinde). Pure data class: SystemConfig key/value haritasından
/// kurulup hem V1 ConstraintEvaluator hem yeni RulePredicate tarafından okunur. Plan 10
/// JSON merge (ConstraintEvaluationService.MergeParameterOverride) bu type'ın property'lerine
/// reflection ile yazar — alan ekleyince hem JSON merge hem evaluator otomatik faydalanır.
/// Faz 5'te V1 evaluator silindiğinde sadece RulePredicate'lar bu config'i okur.
/// </summary>
public sealed class SchedulingConstraintConfig
{
    // Hard constraint params
    public TimeOnly LunchBreakStart { get; set; }
    public TimeOnly LunchBreakEnd { get; set; }
    public int MaxConsecutiveHours { get; set; }
    public int BreakDurationMinutes { get; set; }

    /// <summary>
    /// H-WL — instructor.MaxWeeklyHours=0 (girilmemiş) olduğunda uygulanacak kurumsal varsayılan
    /// haftalık ders saati limiti. V2 WeeklyLoadEncoder ile parite (encoder de override yoksa bunu
    /// uygular); evaluator eskiden 0'ı "limitsiz" sayıp hiç raporlamıyordu (drift).
    /// SystemConfig: <c>Schedule.DefaultInstructorMaxWeeklyHours</c> (default 30).
    /// </summary>
    public int DefaultInstructorMaxWeeklyHours { get; set; } = 30;
    public int AmphiThreshold { get; set; }
    public int TheoryLabSplitThreshold { get; set; }

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
    /// H4 alt-kural 4 flag'i — Course.RequiredTheoryClassroomType / RequiredLabClassroomType
    /// override'ı validator tarafında devreye alır. V2 ClassroomTypeEncoder ile parite
    /// (2026-05-19'a kadar yalnızca solver okuyordu, validator boştu).
    /// SystemConfig: <c>Schedule.ClassroomTypeRequirementEnabled</c> (default false).
    /// </summary>
    public bool ClassroomTypeRequirementEnabled { get; set; } = false;

    /// <summary>
    /// H6 alt-parametre — Tek bir lab bloğunun maksimum saat sayısı (2026-05-19'da
    /// SystemConfigs'ten H6.Parameters JSON'a taşındı; eskiden Schedule.MaxLabBlockHours
    /// SystemConfig key'i, şimdi H6 dialog'unda yönetilir). Default 2.
    /// </summary>
    public int MaxLabBlockHours { get; set; } = 2;

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
            BreakDurationMinutes = GetInt(config, "Schedule.BreakDurationMinutes", 5),

            // Default değerler (kısıt kartı Parameters JSON'u override eder)
            LunchBreakStart = new TimeOnly(11, 40),
            LunchBreakEnd = new TimeOnly(12, 20),
            MaxConsecutiveHours = 4,
            AmphiThreshold = 100,
            TheoryLabSplitThreshold = 4,

            // Plan 11 Faz 1.2 — V2 encoder ile parite kaynaklar
            MaxDailyHours = GetInt(config, "Schedule.MaxDailyHoursPerInstructor", 9),
            DefaultInstructorMaxWeeklyHours = GetInt(config, "Schedule.DefaultInstructorMaxWeeklyHours", 30),
            // 2026-05-19: AmphiThresholdEnabled + ClassroomTypeRequirementEnabled artık
            // H4.Parameters JSON'undan geliyor; ApplyConstraintStateOverrides(state) ile setlensin.

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
            // 2026-05-19: CurriculumConflictMode + EnrollmentConflictHardThreshold artık
            // H5.Parameters JSON'undan ApplyConstraintStateOverrides ile setlenir.
            CurriculumConflictMode = "curriculum",
            EnrollmentConflictHardThreshold = 1,
        };
    }

    /// <summary>
    /// ConstraintState.Definitions Parameters JSON'larından bool/int override'ları okur.
    /// 2026-05-19 — H4 alt-toggle'ları (AmphiThresholdEnabled, ClassroomTypeRequirementEnabled)
    /// SystemConfigs yerine H4 Parameters JSON'unda tutulduğundan FromSystemConfig sonrası
    /// state ile zenginleştirme: caller hem rawConfig'i (SystemConfig dict) hem state'i bilir,
    /// burada per-constraint Parameters JSON parse edilip ilgili field'lar üzerine yazılır.
    /// </summary>
    public void ApplyConstraintStateOverrides(ConstraintState? state)
    {
        if (state == null) return;

        // H4 — Derslik Tipi Uyumu
        if (state.Definitions.TryGetValue("H4", out var h4) && !string.IsNullOrWhiteSpace(h4.Parameters))
        {
            try
            {
                using var doc = JsonDocument.Parse(h4.Parameters!);
                var root = doc.RootElement;
                if (root.TryGetProperty("AmphiThresholdEnabled", out var ate) && ate.ValueKind == JsonValueKind.True)
                    AmphiThresholdEnabled = true;
                else if (root.TryGetProperty("AmphiThresholdEnabled", out ate) && ate.ValueKind == JsonValueKind.False)
                    AmphiThresholdEnabled = false;

                if (root.TryGetProperty("ClassroomTypeRequirementEnabled", out var ctr) && ctr.ValueKind == JsonValueKind.True)
                    ClassroomTypeRequirementEnabled = true;
                else if (root.TryGetProperty("ClassroomTypeRequirementEnabled", out ctr) && ctr.ValueKind == JsonValueKind.False)
                    ClassroomTypeRequirementEnabled = false;

                if (root.TryGetProperty("AmphiThreshold", out var thr) && thr.ValueKind == JsonValueKind.Number && thr.TryGetInt32(out var thrInt))
                    AmphiThreshold = thrInt;
            }
            catch (JsonException) { /* malformed JSON → default'ları koru, silent skip */ }
        }

        // H5 — Müfredat Çakışması: CurriculumConflictMode H5 Parameters JSON'undan
        // (2026-05-19 dual-channel temizliği). Eski SystemConfigs."Schedule.CurriculumConflictMode"
        // yolu kaldırıldı.
        if (state.Definitions.TryGetValue("H5", out var h5) && !string.IsNullOrWhiteSpace(h5.Parameters))
        {
            try
            {
                using var doc = JsonDocument.Parse(h5.Parameters!);
                var root = doc.RootElement;
                if (root.TryGetProperty("CurriculumConflictMode", out var mode) && mode.ValueKind == JsonValueKind.String)
                {
                    var modeStr = mode.GetString();
                    if (!string.IsNullOrWhiteSpace(modeStr))
                        CurriculumConflictMode = modeStr;
                }
                if (root.TryGetProperty("EnrollmentConflictHardThreshold", out var thr) && thr.ValueKind == JsonValueKind.Number && thr.TryGetInt32(out var thrInt))
                    EnrollmentConflictHardThreshold = thrInt;
            }
            catch (JsonException) { /* silent skip */ }
        }

        // H6 — Ders Bileşen Çakışması: MaxLabBlockHours (2026-05-19 SystemConfigs → H6.Parameters).
        if (state.Definitions.TryGetValue("H6", out var h6) && !string.IsNullOrWhiteSpace(h6.Parameters))
        {
            try
            {
                using var doc = JsonDocument.Parse(h6.Parameters!);
                if (doc.RootElement.TryGetProperty("MaxLabBlockHours", out var max)
                    && max.ValueKind == JsonValueKind.Number
                    && max.TryGetInt32(out var maxInt) && maxInt > 0)
                {
                    MaxLabBlockHours = maxInt;
                }
            }
            catch (JsonException) { /* silent skip */ }
        }
    }

    private static bool GetBool(IReadOnlyDictionary<string, string> config, string key, bool defaultValue) =>
        config.TryGetValue(key, out var v) && bool.TryParse(v, out var r) ? r : defaultValue;

    private static int GetInt(IReadOnlyDictionary<string, string> config, string key, int defaultValue) =>
        config.TryGetValue(key, out var v) && int.TryParse(v, out var r) ? r : defaultValue;

    private static double GetDouble(IReadOnlyDictionary<string, string> config, string key, double defaultValue) =>
        config.TryGetValue(key, out var v) && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : defaultValue;
}
