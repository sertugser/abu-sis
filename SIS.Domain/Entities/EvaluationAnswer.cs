namespace SIS.Domain.Entities;

public class EvaluationAnswer
{
    public int Id { get; set; }
    public int ResponseId { get; set; }
    public EvaluationResponse Response { get; set; } = null!;
    public int QuestionId { get; set; }
    public EvaluationQuestion Question { get; set; } = null!;
    public int? RatingValue { get; set; }
    public string? TextValue { get; set; }
}
