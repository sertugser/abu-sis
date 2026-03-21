namespace SIS.Application.Scheduling.DTOs;

public class ScheduleSuggestionDto
{
    public bool IsFeasible { get; set; }
    public int SectionId { get; set; }
    public int? InstructorId { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public int? SuggestedClassroomId { get; set; }
    public string SuggestedClassroomName { get; set; } = string.Empty;
    public List<ScheduleSlotDto> SuggestedSlots { get; set; } = new();
    public decimal EstimatedWeeklyHoursImpact { get; set; }
    public List<string> Notes { get; set; } = new();
    public List<InstructorAvailabilityDto> InstructorAvailabilities { get; set; } = new();
}
