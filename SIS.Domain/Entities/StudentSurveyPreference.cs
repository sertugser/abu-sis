namespace SIS.Domain.Entities;

public class StudentSurveyPreference
{
    public int Id { get; set; }
    public int SurveyId { get; set; }
    public CourseIntentionSurvey Survey { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public string PreferredDays { get; set; } = string.Empty;
    public string PreferredTimeRanges { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
