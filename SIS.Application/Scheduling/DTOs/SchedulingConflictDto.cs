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
    /// <summary>
    /// .NET DayOfWeek as integer (0=Sunday, 1=Monday, …, 6=Saturday). int matches frontend
    /// `day: number | null` contract.
    /// </summary>
    public int? Day { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Message { get; set; } = string.Empty;
}
