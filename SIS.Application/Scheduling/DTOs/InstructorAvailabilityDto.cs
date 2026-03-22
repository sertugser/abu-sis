namespace SIS.Application.Scheduling.DTOs;

public class InstructorAvailabilityDto
{
    public int Id { get; set; }
    public int InstructorId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsPreferred { get; set; }
}
