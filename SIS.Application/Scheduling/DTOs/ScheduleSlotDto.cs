namespace SIS.Application.Scheduling.DTOs;

public class ScheduleSlotDto
{
    /// <summary>
    /// .NET DayOfWeek as integer (0=Sunday, 1=Monday, …, 6=Saturday). Stored as int (not enum)
    /// to keep request/response wire format consistent with frontend `day: number`.
    /// JsonStringEnumConverter would otherwise serialize enum as "Monday" string and break
    /// frontend equality checks.
    /// </summary>
    public int Day { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int? ClassroomId { get; set; }
    public string? ClassroomName { get; set; }
}
