namespace SIS.Application.Scheduling.DTOs;

public class ScheduleValidationResultDto
{
    public bool IsValid { get; set; }
    public List<SchedulingConflictDto> Conflicts { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}
