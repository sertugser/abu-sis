using SIS.Application.Recommendation.DTOs;

namespace SIS.Application.Recommendation.Services;

public interface IRecommendationService
{
    Task<StudentRecommendationsDto> GetStudentRecommendationsAsync(
        int studentId,
        int pacePreference = 50,
        CancellationToken cancellationToken = default);
}
