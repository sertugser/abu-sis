namespace SIS.Application.Scheduling.DTOs;

public class BatchAutoScheduleItemDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ClassroomName { get; set; }
    public List<ScheduleSlotDto>? Slots { get; set; }
    public string? FailureReason { get; set; }
}
