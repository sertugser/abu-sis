namespace SIS.Application.Scheduling.DTOs;

public class TimetableEntryDto
{
    /// <summary>.NET DayOfWeek as integer (0=Sun, 1=Mon, …). int matches frontend wire format.</summary>
    public int Day { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int SectionId { get; set; }
    public int SectionNumber { get; set; }
    public string? ClassroomName { get; set; }
    public string? InstructorName { get; set; }
}
