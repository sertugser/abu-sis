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
        var appliedDraft = await GetLatestAppliedCalendarDraftAsync();
        if (appliedDraft is null)
            return null;

        var draft = appliedDraft.Value;
        var targetYear = TryParseAcademicYearStart(draft.AcademicYear);
        if (!targetYear.HasValue)
            return null;

        var terms = await _context.AcademicTerms
            .AsNoTracking()
            .Include(t => t.CalendarEvents.Where(e =>
                e.IsPublic
                && e.IsGenerated
                && e.GeneratedDraftId == draft.Id))
            .Where(t => t.Year == targetYear.Value)
            .OrderBy(t => t.TermStart)
            .ThenBy(t => t.Semester)
            .ToListAsync();

        if (terms.Count == 0)
            return null;

        var events = terms
            .SelectMany(t => t.CalendarEvents)
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

        if (events.Count == 0)
            return null;

        var term = terms.FirstOrDefault(t => t.IsActive) ?? terms.First();
        var academicYear = draft.AcademicYear;

        return new ActiveTermCalendarDto(
            term.Id,
            $"{academicYear} Akademik Takvim",
            $"{targetYear.Value % 100:00}-{(targetYear.Value + 1) % 100:00}",
            $"Academic Calendar {academicYear}",
            events
        );
    }

    private async Task<(int Id, string AcademicYear)?> GetLatestAppliedCalendarDraftAsync()
    {
        var draft = await _context.GeneratedCalendarDrafts
            .AsNoTracking()
            .Where(d => d.Status == GeneratedCalendarDraftStatus.Applied)
            .OrderByDescending(d => d.AppliedAt ?? d.UpdatedAt)
            .Select(d => new { d.Id, d.AcademicYear })
            .FirstOrDefaultAsync();

        return draft is null ? null : (draft.Id, draft.AcademicYear);
    }

    private static int? TryParseAcademicYearStart(string? academicYear)
    {
        if (string.IsNullOrWhiteSpace(academicYear))
            return null;

        var start = academicYear.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(start, out var year) ? year : null;
    }
}
