namespace SIS.Domain.Entities;

public class EvaluationResponse
{
    public int Id { get; set; }
    public int EvaluationPeriodId { get; set; }
    public EvaluationPeriod EvaluationPeriod { get; set; } = null!;
    public int CourseSectionId { get; set; }
    public CourseSection CourseSection { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateTime SubmittedAt { get; set; }
    public ICollection<EvaluationAnswer> Answers { get; set; } = new List<EvaluationAnswer>();
}
