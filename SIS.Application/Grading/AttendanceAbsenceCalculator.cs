using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Application.Grading;

/// <summary>
/// Extension helpers for AttendanceMethod enum.
/// </summary>
public static class AttendanceMethodExtensions
{
    /// <summary>
    /// Parses the SystemConfig string value (case-insensitive) to AttendanceMethod.
    /// Returns Separate (default/current behaviour) when the value is missing or unrecognised.
    /// </summary>
    public static AttendanceMethod ParseAttendanceMethod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return AttendanceMethod.Separate;

        return value.Trim().ToLowerInvariant() switch
        {
            "separate" => AttendanceMethod.Separate,
            "totalhours" => AttendanceMethod.TotalHours,
            "passive" => AttendanceMethod.Passive,
            "markonly" => AttendanceMethod.MarkOnly,
            _ => AttendanceMethod.Separate,
        };
    }
}

/// <summary>
/// Pure static helpers for computing per-registration absence hours.
/// Single source of truth shared by CourseRegistrationService (student-facing
/// attendance status) and GradeController.Finalize (NA grade assignment).
/// </summary>
public static class AttendanceAbsenceCalculator
{
    /// <summary>
    /// Slot metadata used when projecting SectionSchedule rows into an ordered list.
    /// SlotNumber is 1-based; BlockType indicates Theory or Lab.
    /// </summary>
    public sealed record AttendanceSlotInfo(int SlotNumber, ScheduleBlockType BlockType);

    /// <summary>
    /// Computes theory and lab absence hours for a single registration.
    /// </summary>
    public static (int TheoryHours, int LabHours) CalculateAbsenceHours(
        IReadOnlyCollection<AttendanceRecord> records,
        int theoryHours,
        int labHours,
        int? applicationHours,
        IReadOnlyList<AttendanceSlotInfo>? scheduleSlots)
    {
        var theoryAbsenceHours = 0;
        var labAbsenceHours = 0;
        var fallbackSlotCount = ResolveAttendanceSlotCount(theoryHours, labHours, applicationHours, scheduleSlots?.Count ?? 0);

        foreach (var record in records)
        {
            var totalSlotCount = Math.Max(1, Math.Max(record.TotalSlotCount, fallbackSlotCount));
            foreach (var slotNumber in GetAbsentSlotNumbers(record, totalSlotCount))
            {
                var blockType = scheduleSlots?.FirstOrDefault(slot => slot.SlotNumber == slotNumber)?.BlockType
                    ?? InferAttendanceSlotBlock(slotNumber, theoryHours);

                if (blockType == ScheduleBlockType.Theory)
                    theoryAbsenceHours++;
                else
                    labAbsenceHours++;
            }
        }

        return (theoryAbsenceHours, labAbsenceHours);
    }

    /// <summary>
    /// Calculates the maximum allowed absence hours given the total hours and limit percentage.
    /// Uses ceiling so the threshold is strict (any hours above this count is a failure).
    /// Intentional: limit uses Math.Ceiling and failure is strictly-greater-than (a student may
    /// miss UP TO the limit hours) — preserved from the original attendance display logic for consistency.
    /// </summary>
    public static int CalculateLimitHours(int totalHours, decimal limitPercent)
    {
        if (totalHours <= 0 || limitPercent <= 0m)
            return 0;

        return (int)Math.Ceiling(totalHours * limitPercent / 100m);
    }

    /// <summary>
    /// Returns true when absence hours exceed the limit in either theory or lab.
    /// Uses the Separate method (current/default behaviour): fails if theoryAbs > theoryLimit OR labAbs > labLimit.
    /// Intentional: failure is strictly-greater-than (a student may miss UP TO the limit hours)
    /// — preserved from the original attendance display logic for consistency.
    /// </summary>
    public static bool IsFailedByAbsence(
        int theoryAbsenceHours, int labAbsenceHours,
        int theoryLimitHours, int labLimitHours)
        => theoryAbsenceHours > theoryLimitHours || labAbsenceHours > labLimitHours;

    /// <summary>
    /// Method-aware absence-fail check. Uses the global AttendanceMethod setting to decide behaviour:
    /// <list type="bullet">
    ///   <item><description><b>Separate</b> (default): fails if theoryAbs &gt; theoryLimit OR labAbs &gt; labLimit.</description></item>
    ///   <item><description><b>TotalHours</b>: fails if (theoryAbs + labAbs) &gt; totalLimit.</description></item>
    ///   <item><description><b>Passive</b>: never fails (attendance tracking is off).</description></item>
    ///   <item><description><b>MarkOnly</b>: never fails (informational recording only).</description></item>
    /// </list>
    /// </summary>
    public static bool IsFailedByAbsence(
        AttendanceMethod method,
        int theoryAbsenceHours,
        int labAbsenceHours,
        int theoryLimitHours,
        int labLimitHours,
        int totalLimitHours)
        => method switch
        {
            AttendanceMethod.Passive => false,
            AttendanceMethod.MarkOnly => false,
            AttendanceMethod.TotalHours => (theoryAbsenceHours + labAbsenceHours) > totalLimitHours,
            _ /* Separate */ => theoryAbsenceHours > theoryLimitHours || labAbsenceHours > labLimitHours,
        };

    /// <summary>
    /// Maps DayOfWeek to Monday-anchored day index (Monday = 0 … Sunday = 6).
    /// Used for ordering SectionSchedule slots chronologically.
    /// </summary>
    public static int DaysFromMonday(DayOfWeek day) => ((int)day + 6) % 7;

    // ── private helpers ──────────────────────────────────────────────────────

    private static List<int> GetAbsentSlotNumbers(AttendanceRecord record, int totalSlotCount)
    {
        if (record.Status == AttendanceStatus.Absent)
            return Enumerable.Range(1, Math.Max(1, totalSlotCount)).ToList();

        if (record.Status != AttendanceStatus.Present)
            return [];

        var presentSlots = ParseAttendanceSlotNumbers(record.PresentSlotNumbersCsv, totalSlotCount);
        if (presentSlots.Count == 0)
            return [];

        var presentSet = presentSlots.ToHashSet();
        return Enumerable.Range(1, Math.Max(1, totalSlotCount))
            .Where(slotNumber => !presentSet.Contains(slotNumber))
            .ToList();
    }

    private static List<int> ParseAttendanceSlotNumbers(string? value, int totalSlotCount)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var maxSlot = Math.Max(1, totalSlotCount);
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var slotNumber) ? slotNumber : 0)
            .Where(slotNumber => slotNumber >= 1 && slotNumber <= maxSlot)
            .Distinct()
            .OrderBy(slotNumber => slotNumber)
            .ToList();
    }

    private static int ResolveAttendanceSlotCount(
        int theoryHours,
        int labHours,
        int? applicationHours,
        int scheduledSlotCount)
    {
        if (scheduledSlotCount > 0)
            return scheduledSlotCount;

        return Math.Max(1, Math.Max(0, theoryHours) + Math.Max(0, labHours) + Math.Max(0, applicationHours ?? 0));
    }

    private static ScheduleBlockType InferAttendanceSlotBlock(int slotNumber, int theoryHours) =>
        slotNumber <= Math.Max(0, theoryHours) ? ScheduleBlockType.Theory : ScheduleBlockType.Lab;
}
