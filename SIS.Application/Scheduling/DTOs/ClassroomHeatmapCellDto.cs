using System.Collections.Generic;

namespace SIS.Application.Scheduling.DTOs;

public class ClassroomHeatmapCellDto
{
    public int Day { get; set; }
    public int Hour { get; set; }
    public double Occupancy { get; set; }
    public int UsedClassrooms { get; set; }
    public string Details { get; set; } = string.Empty;
    public List<ClassroomOccupationDto> Occupations { get; set; } = new();
}

public class ClassroomOccupationDto
{
    public string ClassroomName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int EnrolledCount { get; set; }
    public int Capacity { get; set; }
}
