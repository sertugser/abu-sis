using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.FacultyPlanning.Services;

public interface IFacultyPlanningService
{
    Task<FacultyPlanningOverviewDto> GetOverviewAsync(int facultyId, int termId, int? scopeDepartmentId = null, CancellationToken ct = default);

    Task<FacultyPlanningSectionPageDto> GetSectionsAsync(
        int facultyId, int termId, int? departmentId = null, string? status = null,
        int page = 1, int pageSize = 50, string? search = null, CancellationToken ct = default);

    Task<AssignmentResultDto> AssignSectionAsync(AssignSectionRequest request, string userId, CancellationToken ct = default);

    Task UnassignSectionAsync(int sectionId, string userId, CancellationToken ct = default);

    Task<AutoSchedulePreviewDto> AutoSchedulePreviewAsync(AutoScheduleRequest request, string userId, CancellationToken ct = default);

    Task<List<ScheduleRunSummaryDto>> GetScheduleRunsAsync(int facultyId, int termId, CancellationToken ct = default);

    Task<ScheduleRunDetailDto?> GetScheduleRunAsync(int id, CancellationToken ct = default);

    Task<ApplyScheduleResultDto> ApplyScheduleAsync(ApplyScheduleRequest request, string userId, CancellationToken ct = default);

    Task<List<AvailableClassroomDto>> GetAvailableClassroomsAsync(
        int termId, DayOfWeek? day = null, TimeOnly? startTime = null, TimeOnly? endTime = null,
        int? minCapacity = null, string? type = null, CancellationToken ct = default);

    Task<List<SchedulingConflictDto>> GetConflictsAsync(int facultyId, int termId, CancellationToken ct = default);

    Task<List<FacultyPlanningTimeSlotDto>> GetTimeSlotsAsync(int termId, CancellationToken ct = default);

    Task<int> ResolveFacultyForUserAsync(string userId, CancellationToken ct = default);

    Task<int?> ResolveFacultyIdForSectionAsync(int sectionId, CancellationToken ct = default);
}
