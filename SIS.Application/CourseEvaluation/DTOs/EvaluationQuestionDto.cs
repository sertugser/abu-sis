using SIS.Domain.Enums;

namespace SIS.Application.CourseEvaluation.DTOs;

public record EvaluationQuestionDto(
    int Id,
    string Text,
    EvaluationQuestionType QuestionType,
    EvaluationQuestionCategory Category,
    int SortOrder,
    bool IsActive,
    bool IsDeleted);

public record CreateEvaluationQuestionDto(
    string Text,
    EvaluationQuestionType QuestionType,
    EvaluationQuestionCategory Category,
    int SortOrder);

public record UpdateEvaluationQuestionDto(
    string Text,
    EvaluationQuestionType QuestionType,
    EvaluationQuestionCategory Category,
    int SortOrder,
    bool IsActive);
