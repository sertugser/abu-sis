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

// ─── Export ─────────────────────────────────────────────────────────────

public record DepartmentExportDataDto(
    string DepartmentName,
    string TermName,
    int TotalSections,
    int EvaluatedSections,
    decimal OverallResponseRate,
    decimal? AverageRating,
    List<SectionExportRowDto> Sections,
    List<ExportQuestionHeaderDto> RatingQuestions);

public record SectionExportRowDto(
    string CourseCode,
    string CourseName,
    string InstructorName,
    int ResponseCount,
    int EnrolledCount,
    decimal ResponseRate,
    decimal? AverageRating,
    bool IsVisible,
    List<decimal?> QuestionAverages);

public record ExportQuestionHeaderDto(
    int QuestionId,
    string Text,
    EvaluationQuestionCategory Category);
