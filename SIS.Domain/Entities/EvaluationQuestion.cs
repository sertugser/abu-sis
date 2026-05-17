using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class EvaluationQuestion
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public EvaluationQuestionType QuestionType { get; set; }
    public EvaluationQuestionCategory Category { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
}
