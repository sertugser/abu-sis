using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IOsymRegistrationCalendarService
{
    Task<IReadOnlyList<OsymRegistrationCalendarDto>> GetAllAsync(int? year = null, CancellationToken ct = default);
    Task<OsymRegistrationCalendarDto> CreateAsync(CreateOsymRegistrationCalendarDto dto, CancellationToken ct = default);
    Task<OsymRegistrationCalendarDto> UpdateAsync(int id, UpdateOsymRegistrationCalendarDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
