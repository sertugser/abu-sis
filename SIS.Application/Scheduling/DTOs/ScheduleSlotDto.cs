namespace SIS.Application.Scheduling.DTOs;

public class ScheduleSlotDto
{
    public DayOfWeek Day { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int? ClassroomId { get; set; }
    public string? ClassroomName { get; set; }
}
