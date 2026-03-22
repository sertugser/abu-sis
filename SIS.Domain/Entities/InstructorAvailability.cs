namespace SIS.Domain.Entities;

public class InstructorAvailability
{
    public int Id { get; set; }
    public int InstructorId { get; set; }
    public Instructor Instructor { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsPreferred { get; set; }
}
