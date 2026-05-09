namespace SIS.Application.Scheduling.Constraints;

/// <summary>
/// Plan 10 — Constraint UI form parametreleri ↔ SystemConfigs key + DataType eşleşmesi.
/// Admin "Program Kısıtları" sayfasında Parameters JSON form'u doldurunca, ConstraintController
/// bu mapping üzerinden değerleri tek source of truth olan SystemConfigs tablosuna yazar.
/// V2 encoder + V1 evaluator zaten SystemConfigs okuyor; Parameters JSON deprecated.
/// </summary>
/// <remarks>
/// Önceki tasarım: UI Parameters JSON'a yazıyordu, V2 encoder buradan değil SystemConfigs'ten
/// okuyordu → "yetim parameter" sorunu. UI değişikliği auto-schedule davranışını etkilemiyordu.
/// Plan 10 fix: tek kanal — SystemConfigs.
/// </remarks>
public static class ConstraintParameterMapping
{
    public sealed record SystemConfigKey(string Key, string DataType);

    /// <summary>
    /// Constraint code + UI parameter key → SystemConfigs key + DataType.
    /// Code "*" wildcard: parametre constraint-bağımsız (örn. AllowSameCourseSectionOverlap herhangi
    /// bir constraint Parameters JSON'unda görünebilir).
    /// </summary>
    private static readonly Dictionary<(string Code, string UiKey), SystemConfigKey> Map = new()
    {
        // ── H4: Derslik Tipi
        [("H4", "AmphiThreshold")] = new("Schedule.AmphiThreshold", "int"),

        // ── H5: Müfredat Çakışması
        [("H5", "CurriculumConflictMode")] = new("Schedule.CurriculumConflictMode", "string"),
        [("H5", "EnrollmentConflictHardThreshold")] = new("Schedule.EnrollmentHardThreshold", "int"),

        // ── H8: Aynı Ders Şube
        [("H8", "AllowSameCourseSectionOverlap")] = new("Schedule.AllowSameCourseSectionOverlap", "bool"),

        // ── H13: Öğle Arası
        [("H13", "LunchBreakStart")] = new("Schedule.LunchBreakStart", "string"),
        [("H13", "LunchBreakEnd")] = new("Schedule.LunchBreakEnd", "string"),

        // ── H15: Ardışık Limit
        [("H15", "MaxConsecutiveHours")] = new("Schedule.MaxConsecutiveHours", "int"),

        // ── H16: Theory-Lab Sıra (deprecated parametre — semantik değişti, geriye uyum için)
        [("H16", "TheoryLabSplitThreshold")] = new("Schedule.TheoryLabSplitThreshold", "int"),

        // ── H-DL: Günlük Hoca Yükü (Plan 10 Phase 2: S2 ile merge — Category toggle yönetir)
        [("H-DL", "MaxDailyHours")] = new("Schedule.MaxDailyHoursPerInstructor", "int"),
        [("H-DL", "S2MaxDailyHours")] = new("SoftConstraint.S2.MaxDailyHours", "int"),

        // ── S1: Doluluk Oranı
        [("S1", "S1MinOccupancy")] = new("Classroom.OccupancyTarget.Min", "int"),
        [("S1", "S1MaxOccupancy")] = new("Classroom.OccupancyTarget.Max", "int"),

        // ── S3: Slot Yoğunluk
        [("S3", "S3MaxSectionsPerSlot")] = new("SoftConstraint.S3.MaxSectionsPerSlot", "int"),

        // ── S4: Co-Enrollment
        [("S4", "S4MinCoPairThreshold")] = new("SoftConstraint.S4.MinCoPairThreshold", "int"),

        // ── S7: Öğrenci Boşluk
        [("S7", "S7MaxGapSlots")] = new("SoftConstraint.S7.MaxGapSlots", "int"),

        // ── S12: Hafta Dengesi
        [("S12", "S12MaxImbalancePercent")] = new("SoftConstraint.S12.MaxImbalancePercent", "int"),
    };

    /// <summary>
    /// Constraint code + UI parameter key için SystemConfigs key/DataType döner. Mapping yoksa null
    /// (parametre encoder pipeline'a wire'lı değil — yetim, ya deprecated ya henüz implement edilmedi).
    /// </summary>
    public static SystemConfigKey? Resolve(string constraintCode, string uiKey)
    {
        return Map.TryGetValue((constraintCode, uiKey), out var sysKey) ? sysKey : null;
    }

    /// <summary>
    /// Plan 10 — Kategori toggle yapılabilirliği. UI capability map'i ile uyumlu.
    /// 'toggleable': Hard/Soft her ikisi mantıklı, encoder pipeline'da pair var.
    /// 'hard-only': Fizik/mantık gereği zorunlu Hard, Soft yapılamaz.
    /// 'soft-only': Doğası penalty-bazlı, Hard yapılamaz.
    /// </summary>
    public enum CategoryCapability { Toggleable, HardOnly, SoftOnly }

    private static readonly Dictionary<string, CategoryCapability> CategoryMap = new()
    {
        // Hard-only (fizik/mantık)
        ["H1"] = CategoryCapability.HardOnly,
        ["H2"] = CategoryCapability.HardOnly,
        ["H3"] = CategoryCapability.HardOnly,
        ["H6"] = CategoryCapability.HardOnly,
        ["H11"] = CategoryCapability.HardOnly,
        ["H17"] = CategoryCapability.HardOnly,

        // Toggleable
        ["H4"] = CategoryCapability.Toggleable,
        ["H5"] = CategoryCapability.Toggleable,
        ["H8"] = CategoryCapability.Toggleable,
        ["H10"] = CategoryCapability.Toggleable,
        ["H13"] = CategoryCapability.Toggleable,
        ["H15"] = CategoryCapability.Toggleable,
        ["H16"] = CategoryCapability.Toggleable,
        ["H-WL"] = CategoryCapability.Toggleable,
        ["H-DL"] = CategoryCapability.Toggleable,
        ["H-DO"] = CategoryCapability.Toggleable,
        ["DIST"] = CategoryCapability.Toggleable,

        // Soft-only (Plan 10 Phase 2: S2 kaldırıldı, H-DL Category toggle ile yönetiliyor)
        ["S1"] = CategoryCapability.SoftOnly,
        ["S3"] = CategoryCapability.SoftOnly,
        ["S4"] = CategoryCapability.SoftOnly,
        ["S7"] = CategoryCapability.SoftOnly,
        ["S8"] = CategoryCapability.SoftOnly,
        ["S12"] = CategoryCapability.SoftOnly,
        ["S20"] = CategoryCapability.SoftOnly,
    };

    public static CategoryCapability GetCategoryCapability(string code)
    {
        return CategoryMap.TryGetValue(code, out var cap) ? cap : CategoryCapability.HardOnly;
    }
}
