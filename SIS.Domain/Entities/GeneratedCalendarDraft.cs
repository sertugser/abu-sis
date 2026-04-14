using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class GeneratedCalendarDraft
{
    public int Id { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public GeneratedCalendarDraftStatus Status { get; set; } = GeneratedCalendarDraftStatus.Draft;
    public string EventsJson { get; set; } = "[]";
    public string HolidaysJson { get; set; } = "[]";
    public string WarningsJson { get; set; } = "[]";
    public string? PreviousSnapshotJson { get; set; }
    public int QualityScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public int? AppliedByUserId { get; set; }
}
