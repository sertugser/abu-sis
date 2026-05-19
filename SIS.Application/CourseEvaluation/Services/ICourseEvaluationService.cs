using SIS.Application.CourseEvaluation.DTOs;

namespace SIS.Application.CourseEvaluation.Services;

public interface ICourseEvaluationService
{
    Task<EvaluationPeriodDto?> GetActivePeriodAsync(CancellationToken ct = default);

    Task<List<EvaluableCourseDto>> GetMyCoursesAsync(int userId, CancellationToken ct = default);

    Task<List<EvaluationQuestionDto>> GetActiveQuestionsAsync(CancellationToken ct = default);

    Task<(bool Success, string? ErrorMessage)> SubmitEvaluationAsync(int userId, SubmitEvaluationDto dto, CancellationToken ct = default);

    Task<SectionEvaluationResultDto?> GetSectionResultsAsync(int courseSectionId, CancellationToken ct = default);

    Task<DepartmentEvaluationSummaryDto?> GetDepartmentSummaryAsync(int departmentId, int? termId, CancellationToken ct = default);

    Task<EvaluationPeriodDto> OpenPeriodAsync(int termId, DateTime closesAt, string openedBy, CancellationToken ct = default);

    Task<EvaluationPeriodDto> ClosePeriodAsync(int periodId, CancellationToken ct = default);

    Task<List<EvaluationQuestionDto>> GetAllQuestionsAsync(CancellationToken ct = default);

    Task<EvaluationQuestionDto> CreateQuestionAsync(CreateEvaluationQuestionDto dto, CancellationToken ct = default);

    Task<EvaluationQuestionDto?> UpdateQuestionAsync(int id, UpdateEvaluationQuestionDto dto, CancellationToken ct = default);

    Task<bool> DeleteQuestionAsync(int id, CancellationToken ct = default);

    Task<bool> RestoreQuestionAsync(int id, CancellationToken ct = default);

    Task<DepartmentExportDataDto?> GetDepartmentExportDataAsync(int departmentId, int? termId, CancellationToken ct = default);
}
