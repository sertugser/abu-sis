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

    /// <summary>
    /// Backend'de tek formülle hesaplanan haftalık doluluk oranı (%).
    /// Payda = gerçek (akşam dahil) slotsPerDay × ders günü sayısı. Frontend
    /// artık kendi sabit 5×10 formülünü kullanmaz, bu değeri gösterir.
    /// </summary>
    public double WeeklyOccupancyRate { get; set; }

    public List<ClassroomScheduleSlotDto> Schedules { get; set; } = new();
}

public class ClassroomScheduleSlotDto
{
    public int Day { get; set; }
    public int StartHour { get; set; }
    public int EndHour { get; set; }

    /// <summary>Slotun gerçek başlangıç/bitiş saati ("HH:mm"). Haftalık ızgara
    /// satır eşlemesi tam-saat (StartHour/EndHour) yerine bunu kullanır.</summary>
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
}
