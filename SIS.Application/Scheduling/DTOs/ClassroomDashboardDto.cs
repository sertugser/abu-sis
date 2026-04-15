using System.Collections.Generic;

namespace SIS.Application.Scheduling.DTOs;

public class ClassroomDashboardDto
{
    public int TotalClassrooms { get; set; }
    public double OccupancyRate { get; set; }
    public int ConflictsCount { get; set; }
    public int EmptySlots { get; set; }
    public int SlotsPerDay { get; set; }
    public List<ClassroomHeatmapCellDto> Heatmap { get; set; } = new();
    public List<ClassroomInfoDto> Classrooms { get; set; } = new();
}

public class ClassroomInfoDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Type { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public int Floor { get; set; }
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = string.Empty;
    public List<ClassroomScheduleSlotDto> Schedules { get; set; } = new();
}

public class ClassroomScheduleSlotDto
{
    public int Day { get; set; }
    public int StartHour { get; set; }
    public int EndHour { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
}
