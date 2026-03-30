using SIS.Application.Common.DTOs;

namespace SIS.Application.Common.Services;

public interface IAcademicCalendarService
{
    Task<ActiveTermCalendarDto?> GetActiveTermCalendarAsync();
}
