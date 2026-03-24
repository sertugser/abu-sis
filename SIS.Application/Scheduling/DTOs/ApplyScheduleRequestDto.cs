namespace SIS.Application.Scheduling.DTOs;

public class ApplyScheduleRequestDto
{
    public int SectionId { get; set; }
    public int? ClassroomId { get; set; }
    public List<ScheduleSlotDto> ScheduleSlots { get; set; } = new();
}
