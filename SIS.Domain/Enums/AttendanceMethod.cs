namespace SIS.Domain.Enums;

/// <summary>
/// Controls how the absence-based fail check behaves globally.
/// Stored as a string in SystemConfig under key "Attendance.Method".
/// </summary>
public enum AttendanceMethod
{
    /// <summary>
    /// Default: theory and lab absence hours are tracked separately.
    /// A student fails if theoryAbsenceHours > theoryLimitHours OR labAbsenceHours > labLimitHours.
    /// </summary>
    Separate = 0,

    /// <summary>
    /// Theory and lab absence hours are summed into a single total.
    /// A student fails if (theoryAbsenceHours + labAbsenceHours) > totalLimitHours,
    /// where totalLimitHours = CalculateLimitHours(weekCount * (theoryHours + labHours), theoryPercent).
    /// The theory-absence-limit percent is reused as the general percentage for the combined total
    /// because it is the primary (and usually the only non-zero) percent setting in practice.
    /// </summary>
    TotalHours = 1,

    /// <summary>
    /// Attendance system is fully off — no student is ever auto-failed by absence.
    /// Absence records are still accepted but have no effect on grades.
    /// </summary>
    Passive = 2,

    /// <summary>
    /// Attendance is recorded for informational purposes only — no student is auto-failed.
    /// Equivalent to Passive for grade computation; the distinction is UX/reporting intent.
    /// </summary>
    MarkOnly = 3,
}
