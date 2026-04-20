using SIS.Domain.Enums;

namespace SIS.Application.CourseEvaluation.DTOs;

public record SectionEvaluationResultDto(
    int CourseSectionId,
    string CourseCode,
    string CourseName,
    string InstructorName,
    int TotalResponses,
    int EnrolledCount,
    decimal ResponseRate,
    bool IsVisible,
    decimal? OverallAverage,
    List<QuestionResultDto> QuestionResults);

public record QuestionResultDto(
    int QuestionId,
    string QuestionText,
    EvaluationQuestionCategory Category,
    EvaluationQuestionType QuestionType,
    decimal? AverageRating,
    Dictionary<int, int>? RatingDistribution,
    List<string>? TextResponses);
