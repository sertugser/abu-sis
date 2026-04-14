using SIS.Application.CalendarGenerator.DTOs;

namespace SIS.Application.CalendarGenerator.Services;

public interface ICalendarGeneratorService
{
    Task<GenerateCalendarResultDto> GenerateAsync(GenerateCalendarRequestDto request, CancellationToken ct = default);
    Task<CalendarDraftDto> GetDraftAsync(int draftId, CancellationToken ct = default);
    Task<CalendarDraftDto> UpdateDraftEventAsync(int draftId, string draftEventId, UpdateDraftEventDto dto, CancellationToken ct = default);
    Task<WhatIfResultDto> WhatIfAsync(int draftId, WhatIfRequestDto request, CancellationToken ct = default);
    Task<ApplyCalendarDraftResultDto> ApplyAsync(int draftId, ApplyCalendarDraftRequestDto request, int? userId, CancellationToken ct = default);
    Task<ApplyCalendarDraftResultDto> RollbackAsync(int draftId, int? userId, CancellationToken ct = default);
}
