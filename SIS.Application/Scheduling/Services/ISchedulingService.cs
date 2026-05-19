using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.Scheduling.Services;

public interface ISchedulingService
{
    Task<ScoreBreakdownDto> GetScoreBreakdownAsync(int termId, CancellationToken cancellationToken = default);
    Task<List<SchedulingConflictDto>> GetConflictsAsync(int? termId = null, CancellationToken cancellationToken = default);
    Task<List<SchedulingConflictDto>> GetConflictsByDepartmentAsync(int departmentId, int? termId = null, CancellationToken cancellationToken = default);
    Task<ScheduleValidationResultDto> ValidateAsync(ScheduleValidationRequestDto request, CancellationToken cancellationToken = default);
    Task<ScheduleValidationResultDto> ValidateExistingAsync(int? termId = null, CancellationToken cancellationToken = default);
    Task<ScheduleValidationResultDto> ValidateExistingByDepartmentAsync(int departmentId, int? termId = null, CancellationToken cancellationToken = default);
    Task<ScheduleSuggestionDto> SuggestAsync(ScheduleSuggestionRequestDto request, CancellationToken cancellationToken = default);
    Task<ScheduleValidationResultDto> ApplyAsync(ApplyScheduleRequestDto request, CancellationToken cancellationToken = default);
    Task<ClassroomDashboardDto> GetClassroomDashboardAsync(
        int? termId = null,
        int? facultyId = null,
        string? buildingName = null,
        string? classroomType = null,
        int? minCapacity = null,
        int? maxCapacity = null,
        CancellationToken cancellationToken = default);
    Task<ClassroomFilterOptionsDto> GetClassroomFilterOptionsAsync(int termId, CancellationToken cancellationToken = default);
    Task<List<AvailableClassroomResultDto>> SearchAvailableClassroomsAsync(
        int termId,
        List<int> days,
        int startHour,
        int endHour,
        int? minCapacity = null,
        string? classroomType = null,
        int? facultyId = null,
        string? buildingName = null,
        CancellationToken cancellationToken = default);
}
