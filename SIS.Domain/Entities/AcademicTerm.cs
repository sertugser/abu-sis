using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class AcademicTerm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;        // e.g., "2024-2025 Fall"
    public int Year { get; set; }
    public SemesterType Semester { get; set; }
    public DateTime RegistrationStart { get; set; }
    public DateTime RegistrationEnd { get; set; }
    public DateTime AddDropStart { get; set; }
    public DateTime AddDropEnd { get; set; }
    public int WithdrawalDeadlineWeek { get; set; }         // 7 for semester, 14 for annual
    public DateTime TermStart { get; set; }
    public DateTime TermEnd { get; set; }
    public bool IsActive { get; set; }
}
