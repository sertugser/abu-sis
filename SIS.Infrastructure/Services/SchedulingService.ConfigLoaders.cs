using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Infrastructure.Services;

/// <summary>
/// SchedulingService — config yükleyiciler, inner config record'ları, conflict type sabitleri.
/// Buradaki record'lar (SoftConstraintConfig, HardConstraintConfig) tüm partial dosyalardan
/// erişilir; LoadSystemConfigAsync + LoadSoft/HardConstraintConfigAsync de paylaşılır.
/// </summary>
public partial class SchedulingService
{
    internal async Task<Dictionary<string, string>> LoadSystemConfigAsync(CancellationToken ct)
    {
        // 2026-05-09: Eski SchedulingAlgorithmConfigs tablosu SystemConfigs'e merge edildi.
        // Tek tablo, scheduling-related kategorilerle filtrele (HardThreshold, SA, MRV, LNS,
        // Other, Soft, Schedule). Attendance/GPA/Registration vs. ihtiyaç olmadığı için dahil edilmiyor.
        return await _context.SystemConfigs.AsNoTracking()
            .Where(c => c.Category == "HardThreshold"
                     || c.Category == "SA"
                     || c.Category == "MRV"
                     || c.Category == "LNS"
                     || c.Category == "Other"
                     || c.Category == "Soft"
                     || c.Category == "Schedule")
            .ToDictionaryAsync(c => c.Key, c => c.Value, ct);
    }

    internal async Task<SoftConstraintConfig> LoadSoftConstraintConfigAsync(CancellationToken ct)
    {
        // Tek source-of-truth: SchedulingConstraintConfig.FromSystemConfig.
        // Aşağıdaki record sadece SchedulingService'in iç imzaları için adapter.
        var config = await LoadSystemConfigAsync(ct);
        var c = SchedulingConstraintConfig.FromSystemConfig(config);
        return new SoftConstraintConfig(
            c.S1Enabled, c.S1MinOccupancy, c.S1MaxOccupancy,
            c.S2Enabled, c.S2MaxDailyHours,
            c.S3Enabled, c.S3MaxSectionsPerSlot,
            c.S4Enabled, c.S4MinCoPairThreshold,
            c.S5Enabled,
            c.S7Enabled, c.S7MaxGapSlots,
            c.S8Enabled,
            c.S12Enabled, c.S12MaxImbalancePercent);
    }

    /// <summary>
    /// Loads co-enrollment pairs from the last 3 terms (before the given term).
    /// Finds course pairs frequently taken by the same student in the same term.
    /// </summary>
    internal async Task<List<CoEnrollmentPair>> LoadCoEnrollmentPairsAsync(int currentTermId, int minThreshold, CancellationToken ct)
    {
        // Get last 3 term IDs before the current term
        var recentTermIds = await _context.AcademicTerms.AsNoTracking()
            .Where(t => t.Id < currentTermId)
            .OrderByDescending(t => t.Id)
            .Take(3)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (recentTermIds.Count == 0)
            return new List<CoEnrollmentPair>();

        // Get all active registrations from recent terms: (studentId, courseId, termId)
        var registrations = await _context.CourseRegistrations.AsNoTracking()
            .Where(cr => cr.Status == RegistrationStatus.Active
                && cr.CourseSection != null
                && recentTermIds.Contains(cr.CourseSection.TermId))
            .Select(cr => new
            {
                cr.StudentId,
                cr.CourseSection.CourseId,
                cr.CourseSection.TermId
            })
            .ToListAsync(ct);

        // Group by (studentId, termId) to find courses taken together
        var coPairCounts = new Dictionary<(int, int), int>();

        foreach (var termGroup in registrations.GroupBy(r => (r.StudentId, r.TermId)))
        {
            var courseIds = termGroup.Select(r => r.CourseId).Distinct().OrderBy(x => x).ToList();
            for (int i = 0; i < courseIds.Count; i++)
            {
                for (int j = i + 1; j < courseIds.Count; j++)
                {
                    var key = (courseIds[i], courseIds[j]);
                    coPairCounts[key] = coPairCounts.GetValueOrDefault(key) + 1;
                }
            }
        }

        return coPairCounts
            .Where(kvp => kvp.Value >= minThreshold)
            .Select(kvp => new CoEnrollmentPair
            {
                CourseId1 = kvp.Key.Item1,
                CourseId2 = kvp.Key.Item2,
                StudentCount = kvp.Value
            })
            .ToList();
    }

    internal async Task<HardConstraintConfig> LoadHardConstraintConfigAsync(CancellationToken ct)
    {
        // Tek source-of-truth: SchedulingConstraintConfig.FromSystemConfig (adapter).
        var config = await LoadSystemConfigAsync(ct);
        var c = SchedulingConstraintConfig.FromSystemConfig(config);
        return new HardConstraintConfig(
            c.LunchBreakStart, c.LunchBreakEnd,
            c.MaxConsecutiveHours, c.BreakDurationMinutes, c.AmphiThreshold,
            c.TheoryLabSplitThreshold, c.CurriculumConflictMode,
            // Plan 11 — V2 encoder paritesi
            c.MaxDailyHours, c.AmphiThresholdEnabled, c.ClassroomTypeRequirementEnabled);
    }

    private static bool GetBool(Dictionary<string, string> config, string key, bool defaultValue)
    {
        return config.TryGetValue(key, out var value)
            ? bool.TryParse(value, out var result) ? result : defaultValue
            : defaultValue;
    }

    private static int GetInt(Dictionary<string, string> config, string key, int defaultValue)
    {
        return config.TryGetValue(key, out var value)
            ? int.TryParse(value, out var result) ? result : defaultValue
            : defaultValue;
    }

    private static string GetDayOfWeekName(DayOfWeek day) => SchedulingHelpers.GetDayOfWeekName(day);

    // ── Constants & inner types ──────────────────────────────────────────

    internal record SoftConstraintConfig(
        bool S1Enabled, int S1MinOccupancy, int S1MaxOccupancy,
        bool S2Enabled, int S2MaxDailyHours,
        bool S3Enabled, int S3MaxSectionsPerSlot,
        bool S4Enabled, int S4MinCoPairThreshold,
        bool S5Enabled,
        bool S7Enabled, int S7MaxGapSlots,
        bool S8Enabled,
        bool S12Enabled, int S12MaxImbalancePercent);

    internal record HardConstraintConfig(
        TimeOnly LunchBreakStart, TimeOnly LunchBreakEnd,
        int MaxConsecutiveHours, int BreakDurationMinutes, int AmphiThreshold,
        int TheoryLabSplitThreshold = 4,
        string CurriculumConflictMode = "curriculum",
        // Plan 11 — V2 encoder paritesi (Faz 1.2 + 1.4)
        int MaxDailyHours = 9,
        bool AmphiThresholdEnabled = false,
        bool ClassroomTypeRequirementEnabled = false);


    private static class ConflictTypes
    {
        public const string ClassroomConflict = "ClassroomConflict";
        public const string InstructorConflict = "InstructorConflict";
        public const string CapacityConflict = "CapacityConflict";
        public const string WeeklyLoadConflict = "WeeklyLoadConflict";
        public const string InvalidScheduleJson = "InvalidScheduleJson";
        public const string ClassroomTypeConflict = "ClassroomTypeConflict";             // H4
        public const string CurriculumConflict = "CurriculumConflict";                   // H5
        public const string SameCourseComponentConflict = "SameCourseComponentConflict"; // H6
        public const string SameCourseSectionConflict = "SameCourseSectionConflict";     // H8
        public const string FacultyClassroomConflict = "FacultyClassroomConflict";       // H10
        public const string CorequisiteConflict = "CorequisiteConflict";                 // H11
        public const string CreditHourMismatch = "CreditHourMismatch";                   // H12
        public const string LunchBreakConflict = "LunchBreakConflict";                   // H13
        public const string MaxConsecutiveHoursConflict = "MaxConsecutiveHoursConflict"; // H15
        public const string DailyLoadConflict = "DailyLoadConflict";                       // S-DL: günlük 9 saat aşımı
        public const string NoDayOffConflict = "NoDayOffConflict";                         // S-DO: haftalık boş gün yok
    }
}
