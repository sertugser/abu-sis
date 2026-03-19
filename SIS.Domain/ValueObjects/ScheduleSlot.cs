namespace SIS.Domain.ValueObjects;

/// <summary>
/// Represents a single time block in a course section's weekly schedule.
/// Not a database entity — serialized as JSON in CourseSection.ScheduleJson.
/// </summary>
public class ScheduleSlot
{
    public DayOfWeek Day { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
