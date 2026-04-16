using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class InstructorPreference
{
    public int Id { get; set; }
    public int InstructorId { get; set; }
    public PreferenceType PreferenceType { get; set; }
    public PreferenceLevel PreferenceLevel { get; set; }
    public int? DayOfWeek { get; set; }            // 0-6 for TimeSlot and DayOff
    public TimeOnly? TimeSlotStart { get; set; }    // TimeSlot
    public TimeOnly? TimeSlotEnd { get; set; }      // TimeSlot
    public int? ClassroomId { get; set; }           // Classroom preference
    public string? BuildingName { get; set; }       // Building preference
    public int? MaxHours { get; set; }              // DailyLoad
    public string? Reason { get; set; }
    public int? TermId { get; set; }                // null = all terms
    public bool IsApproved { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Instructor Instructor { get; set; } = null!;
    public Classroom? Classroom { get; set; }
    public AcademicTerm? Term { get; set; }
}
