namespace SIS.Application.CourseEvaluation.DTOs;

public record DepartmentEvaluationSummaryDto(
    int DepartmentId,
    string DepartmentName,
    int TermId,
    string TermName,
    int TotalSections,
    int EvaluatedSections,
    decimal OverallResponseRate,
    decimal? AverageRating,
    List<SectionSummaryDto> SectionSummaries);

public record SectionSummaryDto(
    int CourseSectionId,
    string CourseCode,
    string CourseName,
    string InstructorName,
    int ResponseCount,
    int EnrolledCount,
    decimal ResponseRate,
    decimal? AverageRating,
    bool IsVisible);
