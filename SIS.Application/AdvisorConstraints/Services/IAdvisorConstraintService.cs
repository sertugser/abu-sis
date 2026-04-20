using SIS.Application.AdvisorConstraints.DTOs;

namespace SIS.Application.AdvisorConstraints.Services;

public interface IAdvisorConstraintService
{
    Task<List<AdvisorCourseConstraintDto>> GetStudentConstraintsAsync(
        int studentId,
        int? termId = null,
        CancellationToken cancellationToken = default);

    Task<AdvisorCourseConstraintDto> UpsertConstraintAsync(
        int advisorInstructorId,
        UpsertAdvisorCourseConstraintRequest request,
        CancellationToken cancellationToken = default);

    Task<AdvisorCourseConstraintDto?> UpdateConstraintAsync(
        int advisorInstructorId,
        int constraintId,
        UpdateAdvisorCourseConstraintRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteConstraintAsync(
        int advisorInstructorId,
        int constraintId,
        CancellationToken cancellationToken = default);

    Task<EffectiveAdvisorConstraintDto?> GetEffectiveConstraintAsync(
        int studentId,
        int courseId,
        int termId,
        CancellationToken cancellationToken = default);

    Task<Dictionary<int, EffectiveAdvisorConstraintDto>> GetEffectiveConstraintsAsync(
        int studentId,
        IEnumerable<int> courseIds,
        int termId,
        CancellationToken cancellationToken = default);

    Task<HashSet<int>> GetBlockedCourseIdsAsync(
        int studentId,
        int termId,
        CancellationToken cancellationToken = default);
}
