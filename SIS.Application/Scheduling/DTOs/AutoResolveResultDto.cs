namespace SIS.Application.Scheduling.DTOs;

public class AutoResolveResultDto
{
    public int ResolvedCount { get; set; }
    public int RemainingConflictCount { get; set; }
    public List<AutoResolveItemDto> ResolvedSections { get; set; } = new();
    public List<SchedulingConflictDto> RemainingConflicts { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public class AutoResolveItemDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string? NewClassroomName { get; set; }
    public List<ScheduleSlotDto>? NewSlots { get; set; }
}
