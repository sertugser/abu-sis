using SIS.Application.CalendarGenerator.DTOs;

namespace SIS.Application.CalendarGenerator.Services;

public interface ICalendarTemplateService
{
    Task<IReadOnlyList<CalendarTemplateDto>> GetTemplatesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CalendarTemplateDto>> ReplaceTemplatesAsync(IReadOnlyList<UpsertCalendarTemplateDto> templates, CancellationToken ct = default);
    Task<IReadOnlyList<HolidayDefinitionDto>> GetHolidayDefinitionsAsync(CancellationToken ct = default);
    Task<HolidayDefinitionDto> UpsertHolidayDefinitionAsync(int? id, UpsertHolidayDefinitionDto dto, CancellationToken ct = default);
    Task EnsureDefaultsAsync(CancellationToken ct = default);
}
