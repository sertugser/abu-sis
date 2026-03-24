namespace SIS.Application.Scheduling.DTOs;

public class BatchAutoScheduleResultDto
{
    public int TotalSections { get; set; }
    public int ScheduledCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<BatchAutoScheduleItemDto> Results { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}
