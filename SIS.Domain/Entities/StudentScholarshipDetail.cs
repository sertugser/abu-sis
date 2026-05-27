using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

/// <summary>
/// 1-to-many child of <see cref="Student"/> that records a single scholarship
/// held by the student. A student may hold several scholarships simultaneously.
/// NOTE: this entity is INDEPENDENT of <c>Student.IsScholarshipStudent</c> — it
/// does NOT change that boolean and must never auto-sync it.
/// </summary>
public class StudentScholarshipDetail
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public ScholarshipType Type { get; set; }
    public decimal? RatePercent { get; set; }
    public string? Source { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
