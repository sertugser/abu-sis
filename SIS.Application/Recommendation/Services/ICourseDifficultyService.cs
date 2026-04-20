using SIS.Application.Recommendation.DTOs;

namespace SIS.Application.Recommendation.Services;

public interface ICourseDifficultyService
{
    Task<CourseDifficultyDto> GetDifficultyAsync(int courseId, CancellationToken cancellationToken = default);
}
