using Microsoft.EntityFrameworkCore;
using SIS.Application.CalendarGenerator.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.CalendarGenerator;

public partial class CalendarGeneratorService
{
    public async Task<ApplyCalendarDraftResultDto> ApplyAsync(int draftId, ApplyCalendarDraftRequestDto request, int? userId, CancellationToken ct = default)
    {
        var draft = await LoadDraftAsync(draftId, ct);
        EnsureEditable(draft);

        var events = Deserialize<List<GeneratedCalendarEventDto>>(draft.EventsJson);
        if (events.Any(e => e.IsFailed))
            throw new InvalidOperationException("Yerlesemeyen etkinlikler varken taslak uygulanamaz.");
        var blockingWarnings = Deserialize<List<CalendarWarningDto>>(draft.WarningsJson)
            .Where(w => string.Equals(w.Severity, "Error", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (blockingWarnings.Count > 0)
            throw new InvalidOperationException("Yönetmelik veya kronoloji hataları giderilmeden taslak uygulanamaz: " + string.Join(" | ", blockingWarnings.Select(w => w.Message)));

        var startYear = ParseAcademicYearStart(draft.AcademicYear);
        var termsBySemester = await GetMatchingTermsBySemesterAsync(startYear, events, ct);
        var skippedSemesters = events
            .Select(e => e.Semester)
            .Distinct()
            .Where(s => !termsBySemester.ContainsKey(s))
            .OrderBy(s => s)
            .Select(s => (Semester: s, Reason: "eşleşen dönem yok"))
            .ToList();

        var termIds = new List<int>();
        var snapshot = new List<TermSnapshot>();

        await using var tx = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory"
            ? null
            : await _context.Database.BeginTransactionAsync(ct);

        try
        {
            foreach (var group in events.GroupBy(e => e.Semester))
            {
                if (!termsBySemester.TryGetValue(group.Key, out var term))
                    continue;

                if (term.IsCancelled)
                {
                    skippedSemesters.Add((group.Key, "iptal edilmiş"));
                    continue;
                }
                if (term.CalendarEvents.Count > 0 && !request.ForceOverwrite)
                    throw new InvalidOperationException("Takvim zaten uygulanmış dönem için forceOverwrite gerekir.");

                snapshot.Add(TermSnapshot.From(term));
                ApplyTermDates(term, group.ToList());

                _context.TermCalendarEvents.RemoveRange(term.CalendarEvents);
                await _context.SaveChangesAsync(ct);

                foreach (var ev in group.OrderBy(e => e.StartDate).ThenBy(e => e.SortWeight()))
                {
                    await _ruleService.ValidateCalendarEventAsync(term, ev.EventType, ev.StartDate, ev.EndDate, ct);
                    _context.TermCalendarEvents.Add(new TermCalendarEvent
                    {
                        TermId = term.Id,
                        EventType = ev.EventType,
                        Title = ev.Title,
                        StartDate = EnsureUtcDate(ev.StartDate),
                        EndDate = ev.EndDate.HasValue ? EnsureUtcDate(ev.EndDate.Value) : null,
                        IsPublic = ev.IsPublic,
                        Note = ev.Note,
                        IsGenerated = true,
                        ChainCode = ev.ChainCode,
                        TemplateItemKey = ev.TemplateItemKey,
                        GeneratedDraftId = draft.Id,
                        BlocksAcademicEvents = ev.BlocksAcademicEvents
                    });
                }

                term.UpdatedAt = DateTime.UtcNow;
                termIds.Add(term.Id);
            }

            if (termIds.Count == 0)
                throw new InvalidOperationException("Taslaktaki hiçbir aktif dönem uygulanamadı. Atlanan dönemler: " + FormatSkippedSemesterReasons(skippedSemesters) + ".");

            draft.PreviousSnapshotJson = Serialize(snapshot);
            draft.Status = GeneratedCalendarDraftStatus.Applied;
            draft.AppliedAt = DateTime.UtcNow;
            draft.AppliedByUserId = userId;
            draft.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            if (tx != null)
                await tx.CommitAsync(ct);

            var message = skippedSemesters.Count == 0
                ? "Akademik takvim uygulandı."
                : $"Akademik takvim uygun dönemlere uygulandı. Atlanan dönemler: {FormatSkippedSemesterReasons(skippedSemesters)}.";
            return new ApplyCalendarDraftResultDto(true, message, termIds);
        }
        catch
        {
            if (tx != null)
                await tx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<Dictionary<SemesterType, AcademicTerm>> GetMatchingTermsBySemesterAsync(
        int startYear,
        IReadOnlyList<GeneratedCalendarEventDto> events,
        CancellationToken ct)
    {
        var expectedSemesters = events
            .Select(e => e.Semester)
            .Distinct()
            .ToList();

        var terms = await _context.AcademicTerms
            .Include(t => t.CalendarEvents)
            .Include(t => t.AcademicCalendar)
            .Where(t => t.Year == startYear)
            .ToListAsync(ct);

        var map = new Dictionary<SemesterType, AcademicTerm>();
        foreach (var semester in expectedSemesters)
        {
            var termType = semester switch
            {
                SemesterType.Fall => TermType.Fall,
                SemesterType.Spring => TermType.Spring,
                SemesterType.Summer => TermType.Summer,
                _ => TermType.Fall
            };
            var term = terms.FirstOrDefault(t => t.TermType == termType);
            if (term != null)
                map[semester] = term;
        }
        return map;
    }

    private static string FormatSemesterList(IEnumerable<SemesterType> semesters) =>
        string.Join(", ", semesters.Select(s => s switch
        {
            SemesterType.Fall => "Güz",
            SemesterType.Spring => "Bahar",
            SemesterType.Summer => "Yaz",
            _ => s.ToString()
        }));

    private static string FormatSkippedSemesterReasons(IEnumerable<(SemesterType Semester, string Reason)> semesters) =>
        string.Join(", ", semesters
            .OrderBy(item => item.Semester)
            .Select(item => $"{SemesterLabel(item.Semester)} ({item.Reason})"));

    public async Task<ApplyCalendarDraftResultDto> RollbackAsync(int draftId, int? userId, CancellationToken ct = default)
    {
        var draft = await LoadDraftAsync(draftId, ct);
        if (draft.Status != GeneratedCalendarDraftStatus.Applied)
            throw new InvalidOperationException("Sadece uygulanmış taslak geri alınabilir.");

        var snapshots = Deserialize<List<TermSnapshot>>(draft.PreviousSnapshotJson ?? "[]");
        var restored = new List<int>();

        foreach (var snapshot in snapshots)
        {
            var term = await _context.AcademicTerms
                .Include(t => t.CalendarEvents)
                .Include(t => t.AcademicCalendar)
                .FirstOrDefaultAsync(t => t.Id == snapshot.TermId, ct);
            if (term == null || term.IsCancelled)
                continue;

            term.TermStart = snapshot.TermStart;
            term.TermEnd = snapshot.TermEnd;
            term.UpdatedAt = DateTime.UtcNow;

            if (term.AcademicCalendar != null)
            {
                term.AcademicCalendar.RegistrationStart = snapshot.RegistrationStart;
                term.AcademicCalendar.RegistrationEnd = snapshot.RegistrationEnd;
                term.AcademicCalendar.AddDropStart = snapshot.AddDropStart;
                term.AcademicCalendar.AddDropEnd = snapshot.AddDropEnd;
                term.AcademicCalendar.WithdrawalDeadline = snapshot.WithdrawalDeadline;
                term.AcademicCalendar.GradeEntryStart = snapshot.GradeEntryStart;
                term.AcademicCalendar.GradeEntryEnd = snapshot.GradeEntryEnd;
                term.AcademicCalendar.UpdatedAt = DateTime.UtcNow;
            }

            _context.TermCalendarEvents.RemoveRange(term.CalendarEvents);
            foreach (var ev in snapshot.GeneratedEvents)
            {
                _context.TermCalendarEvents.Add(new TermCalendarEvent
                {
                    TermId = term.Id,
                    EventType = ev.EventType,
                    Title = ev.Title,
                    StartDate = ev.StartDate,
                    EndDate = ev.EndDate,
                    IsPublic = ev.IsPublic,
                    Note = ev.Note,
                    IsGenerated = ev.IsGenerated,
                    ChainCode = ev.ChainCode,
                    TemplateItemKey = ev.TemplateItemKey,
                    GeneratedDraftId = ev.GeneratedDraftId,
                    BlocksAcademicEvents = ev.BlocksAcademicEvents
                });
            }

            restored.Add(term.Id);
        }

        draft.Status = GeneratedCalendarDraftStatus.RolledBack;
        draft.UpdatedAt = DateTime.UtcNow;
        draft.AppliedByUserId = userId;
        await _context.SaveChangesAsync(ct);

        return new ApplyCalendarDraftResultDto(true, "Akademik takvim geri alındı.", restored);
    }

    private record TermSnapshot(
        int TermId,
        DateTime RegistrationStart,
        DateTime RegistrationEnd,
        DateTime AddDropStart,
        DateTime AddDropEnd,
        DateTime WithdrawalDeadline,
        DateTime TermStart,
        DateTime TermEnd,
        DateTime GradeEntryStart,
        DateTime GradeEntryEnd,
        IReadOnlyList<TermCalendarEventSnapshot> GeneratedEvents)
    {
        public static TermSnapshot From(AcademicTerm term) =>
            new(
                term.Id,
                term.AcademicCalendar?.RegistrationStart ?? term.TermStart,
                term.AcademicCalendar?.RegistrationEnd ?? term.TermStart,
                term.AcademicCalendar?.AddDropStart ?? term.TermStart,
                term.AcademicCalendar?.AddDropEnd ?? term.TermStart,
                term.AcademicCalendar?.WithdrawalDeadline ?? term.TermEnd,
                term.TermStart,
                term.TermEnd,
                term.AcademicCalendar?.GradeEntryStart ?? term.TermEnd,
                term.AcademicCalendar?.GradeEntryEnd ?? term.TermEnd,
                term.CalendarEvents.Select(TermCalendarEventSnapshot.From).ToList());
    }

    private record TermCalendarEventSnapshot(
        CalendarEventType EventType,
        string Title,
        DateTime StartDate,
        DateTime? EndDate,
        bool IsPublic,
        string? Note,
        string? ChainCode,
        string? TemplateItemKey,
        int? GeneratedDraftId,
        bool BlocksAcademicEvents,
        bool IsGenerated)
    {
        public static TermCalendarEventSnapshot From(TermCalendarEvent ev) =>
            new(ev.EventType, ev.Title, ev.StartDate, ev.EndDate, ev.IsPublic, ev.Note, ev.ChainCode, ev.TemplateItemKey, ev.GeneratedDraftId, ev.BlocksAcademicEvents, ev.IsGenerated);
    }
}
