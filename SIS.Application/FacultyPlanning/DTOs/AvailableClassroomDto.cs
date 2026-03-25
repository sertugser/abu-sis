namespace SIS.Application.FacultyPlanning.DTOs;

public class AvailableClassroomDto
{
    public int ClassroomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal FitScore { get; set; }
}
