namespace SIS.Domain.Entities;

public class ScheduleRun
{
    public int Id { get; set; }
    public int FacultyId { get; set; }
    public Faculty Faculty { get; set; } = null!;
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public int Seed { get; set; }
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public double TotalPenalty { get; set; }
    public double ElapsedSeconds { get; set; }
    public string ResultJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
}
