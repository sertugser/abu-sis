namespace SIS.Application.GpaCalculation.DTOs;

public class TermGpaHistoryItemDto
{
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Semester { get; set; } = string.Empty;
    public int CourseCount { get; set; }
    public int TotalCredits { get; set; }
    public int TotalEcts { get; set; }
    public decimal Sgpa { get; set; }
    public decimal Cgpa { get; set; }
}
