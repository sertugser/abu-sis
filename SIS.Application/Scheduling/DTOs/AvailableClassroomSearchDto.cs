namespace SIS.Application.Scheduling.DTOs;

public class AvailableClassroomResultDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Type { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public int Floor { get; set; }
    public double WeeklyOccupancyRate { get; set; }
    public List<ClassroomScheduleSlotDto> Schedules { get; set; } = new();
}
