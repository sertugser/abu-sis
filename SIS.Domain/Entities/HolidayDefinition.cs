using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class HolidayDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public HolidayType Type { get; set; }
    public int? FixedMonth { get; set; }
    public int? FixedDay { get; set; }
    public int? HijriMonth { get; set; }
    public int? HijriDay { get; set; }
    public decimal DurationDays { get; set; }
    public bool IsHalfDayStart { get; set; }
    public bool IsOfficialHoliday { get; set; } = true;
    public bool BlocksAcademicEvents { get; set; } = true;
    public string Source { get; set; } = string.Empty;
    public string OverridesJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
