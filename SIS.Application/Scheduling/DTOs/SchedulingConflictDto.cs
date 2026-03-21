namespace SIS.Application.Scheduling.DTOs;

public class SchedulingConflictDto
{
    public string ConflictType { get; set; } = string.Empty;
    public int SectionId { get; set; }
    public int? RelatedSectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int? InstructorId { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public int? ClassroomId { get; set; }
    public string ClassroomName { get; set; } = string.Empty;
    public DayOfWeek? Day { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Message { get; set; } = string.Empty;
}
