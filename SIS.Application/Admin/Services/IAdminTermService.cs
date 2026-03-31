using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IAdminTermService
{
    Task<List<AdminTermDto>> GetAllTermsAsync();
    Task<AdminTermDto> GetTermByIdAsync(int id);
    Task<AdminTermDto> CreateTermAsync(CreateTermDto dto);
    Task<AdminTermDto> UpdateTermAsync(int id, UpdateTermDto dto);
    Task ActivateTermAsync(int id);
    Task TransitionStatusAsync(int id, string targetStatus);

    Task<List<TermCalendarEventDto>> GetCalendarEventsAsync(int termId);
    Task<TermCalendarEventDto> CreateCalendarEventAsync(int termId, CreateCalendarEventDto dto);
    Task<TermCalendarEventDto> UpdateCalendarEventAsync(int termId, int eventId, UpdateCalendarEventDto dto);
    Task DeleteCalendarEventAsync(int termId, int eventId);
}
