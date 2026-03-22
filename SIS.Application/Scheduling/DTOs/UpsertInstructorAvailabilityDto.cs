namespace SIS.Application.Scheduling.DTOs;

public class UpsertInstructorAvailabilityDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsPreferred { get; set; }
}
