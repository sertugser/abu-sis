namespace SIS.Application.Scheduling.DTOs;

public class TimetableEntryDto
{
    public DayOfWeek Day { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int SectionId { get; set; }
    public int SectionNumber { get; set; }
    public string? ClassroomName { get; set; }
    public string? InstructorName { get; set; }
}
