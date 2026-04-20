namespace SIS.Application.CourseEvaluation.DTOs;

public record SubmitEvaluationDto(
    int CourseSectionId,
    List<EvaluationAnswerDto> Answers);

public record EvaluationAnswerDto(
    int QuestionId,
    int? RatingValue,
    string? TextValue);
