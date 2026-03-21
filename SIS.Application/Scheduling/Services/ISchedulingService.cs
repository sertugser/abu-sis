using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.Scheduling.Services;

public interface ISchedulingService
{
    Task<List<SchedulingConflictDto>> GetConflictsAsync(int? termId = null, CancellationToken cancellationToken = default);
    Task<ScheduleValidationResultDto> ValidateAsync(ScheduleValidationRequestDto request, CancellationToken cancellationToken = default);
    Task<ScheduleValidationResultDto> ValidateExistingAsync(int? termId = null, CancellationToken cancellationToken = default);
    Task<ScheduleSuggestionDto> SuggestAsync(ScheduleSuggestionRequestDto request, CancellationToken cancellationToken = default);
}
