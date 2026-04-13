using SIS.Application.CourseIntention.DTOs;

namespace SIS.Application.CourseIntention.Services;

public interface ICourseIntentionService
{
    Task<CourseIntentionSurveyDetailDto?> GetActiveSurveyForStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<CourseIntentionSurveyDetailDto?> GetSurveyForStudentAsync(
        int studentId,
        int surveyId,
        CancellationToken cancellationToken = default);

    Task<CourseIntentionSurveyDetailDto> SaveResponsesAsync(
        int studentId,
        SaveCourseIntentionsRequest request,
        CancellationToken cancellationToken = default);

    Task<List<CourseIntentionSurveyDto>> GetSurveysAsync(
        int? termId = null,
        CancellationToken cancellationToken = default);

    Task<CourseIntentionSurveyDto> UpsertSurveyAsync(
        UpsertCourseIntentionSurveyRequest request,
        CancellationToken cancellationToken = default);

    Task<CourseIntentionSurveyDto?> SetSurveyStateAsync(
        int surveyId,
        SetCourseIntentionSurveyStateRequest request,
        CancellationToken cancellationToken = default);

    Task<CourseIntentionSummaryDto?> GetSurveySummaryAsync(
        int surveyId,
        int? departmentId = null,
        CancellationToken cancellationToken = default);
}
