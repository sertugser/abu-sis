using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class CourseIntention
{
    public int Id { get; set; }
    public int SurveyId { get; set; }
    public CourseIntentionSurvey Survey { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public CourseIntentionLevel Intention { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
