namespace SIS.Application.Scheduling.DTOs;

public class ScheduleValidationRequestDto
{
    public int SectionId { get; set; }
    public int? InstructorId { get; set; }
    public int? ClassroomId { get; set; }
    public List<ScheduleSlotDto>? ScheduleSlots { get; set; }
}
