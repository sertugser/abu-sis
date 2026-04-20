using SIS.Application.CalendarGenerator.DTOs;

namespace SIS.Application.CalendarGenerator.Services;

public interface IHolidayCalculatorService
{
    Task<IReadOnlyList<ResolvedHolidayDto>> ResolveHolidaysAsync(int startYear, int endYear, CancellationToken ct = default);
}
