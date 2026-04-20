using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class CourseIntentionSurvey
{
    public int Id { get; set; }
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public CourseIntentionSurveyStatus Status { get; set; } = CourseIntentionSurveyStatus.Scheduled;
    public DateTime OpenAtUtc { get; set; }
    public DateTime CloseAtUtc { get; set; }
    public bool IsManualOverride { get; set; }
    public DateTime? ManualOverrideAtUtc { get; set; }
    public DateTime? LastStatusChangedAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<CourseIntention> Intentions { get; set; } = new List<CourseIntention>();
    public ICollection<StudentSurveyPreference> Preferences { get; set; } = new List<StudentSurveyPreference>();
}
