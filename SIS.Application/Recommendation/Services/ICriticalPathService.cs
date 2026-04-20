using SIS.Application.Recommendation.DTOs;

namespace SIS.Application.Recommendation.Services;

public interface ICriticalPathService
{
    Task<CriticalPathAnalysisDto> AnalyzeCourseAsync(
        int studentId,
        int courseId,
        CancellationToken cancellationToken = default);
}
