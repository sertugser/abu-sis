using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class AcademicTerm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;        // e.g., "2024-2025 Bahar"
    public string ShortName { get; set; } = string.Empty;   // e.g., "25-26 Bahar"
    public string EnglishName { get; set; } = string.Empty; // e.g., "Spring 2025-2026"
    public int Year { get; set; }
    public SemesterType Semester { get; set; }
    public TermType TermType { get; set; } = TermType.Regular;
    public DateTime RegistrationStart { get; set; }
    public DateTime RegistrationEnd { get; set; }
    public DateTime AddDropStart { get; set; }
    public DateTime AddDropEnd { get; set; }
    public DateTime WithdrawalDeadline { get; set; }
    public DateTime TermStart { get; set; }
    public DateTime TermEnd { get; set; }
    public bool IsActive { get; set; }

    public ICollection<TermCalendarEvent> CalendarEvents { get; set; } = new List<TermCalendarEvent>();
}
