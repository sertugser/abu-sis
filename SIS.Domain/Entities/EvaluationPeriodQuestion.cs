namespace SIS.Domain.Entities;

public class EvaluationPeriodQuestion
{
    public int EvaluationPeriodId { get; set; }
    public int EvaluationQuestionId { get; set; }

    public EvaluationPeriod Period { get; set; } = null!;
    public EvaluationQuestion Question { get; set; } = null!;
}
