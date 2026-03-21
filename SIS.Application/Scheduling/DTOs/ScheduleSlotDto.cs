namespace SIS.Application.Scheduling.DTOs;

public class ScheduleSlotDto
{
    public DayOfWeek Day { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
