using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.DTOs;
using SIS.Application.Common.Services;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AcademicCalendarService : IAcademicCalendarService
{
    private readonly AppDbContext _context;

    public AcademicCalendarService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ActiveTermCalendarDto?> GetActiveTermCalendarAsync()
    {
        var term = await _context.AcademicTerms
            .Include(t => t.CalendarEvents.Where(e => e.IsPublic))
            .FirstOrDefaultAsync(t => t.IsActive);

        if (term is null)
            return null;

        var events = term.CalendarEvents
            .OrderBy(e => e.StartDate)
            .Select(e => new PublicCalendarEventDto(
                e.Id,
                e.EventType.ToString(),
                CalendarEventTypeLabels.GetLabel(e.EventType),
                e.Title,
                e.StartDate,
                e.EndDate,
                e.Note
            ))
            .ToList();

        return new ActiveTermCalendarDto(
            term.Id,
            term.Name,
            term.ShortName,
            term.EnglishName,
            events
        );
    }
}
