using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.FacultyPlanning.DTOs;

public class AssignSectionRequest
{
    public int SectionId { get; set; }
    public int? ClassroomId { get; set; }
    public List<ScheduleSlotDto>? ScheduleSlots { get; set; }
    public int? InstructorId { get; set; }
}

public class AssignmentResultDto
{
    public bool Success { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<SchedulingConflictDto> Conflicts { get; set; } = new();
}
