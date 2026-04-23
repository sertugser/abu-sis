using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicCalendar.DTOs;
using SIS.Application.AcademicCalendar.Services;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AcademicCalendarService : IAcademicCalendarService
{
    private readonly AppDbContext _ctx;
    private readonly IDateTimeProvider _clock;

    public AcademicCalendarService(AppDbContext ctx, IDateTimeProvider clock)
    {
        _ctx   = ctx;
        _clock = clock;
    }

    // ── Admin ─────────────────────────────────────────────────────────

    public async Task<AcademicCalendarDto?> GetByTermIdAsync(int termId, CancellationToken ct = default)
    {
        var term = await _ctx.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new KeyNotFoundException($"Term {termId} not found.");

        if (term.AcademicCalendar is null)
            return null;

        return ToDto(term, term.AcademicCalendar);
    }

    public async Task<AcademicCalendarDto> UpsertAsync(int termId, UpsertAcademicCalendarDto dto, CancellationToken ct = default)
    {
        var term = await _ctx.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new KeyNotFoundException($"Term {termId} not found.");

        if (term.IsCancelled)
            throw new InvalidOperationException("Bu dönem iptal edilmiş; takvim oluşturulamaz.");

        Validate(dto, term);

        var isNew = term.AcademicCalendar is null;
        var cal = term.AcademicCalendar ?? new TermAcademicCalendar
        {
            TermId    = term.Id,
            CreatedAt = _clock.UtcNow
        };

        if (!isNew) cal.UpdatedAt = _clock.UtcNow;

        cal.RegistrationStart  = DateTime.SpecifyKind(dto.RegistrationStart,  DateTimeKind.Utc);
        cal.RegistrationEnd    = DateTime.SpecifyKind(dto.RegistrationEnd,    DateTimeKind.Utc);
        cal.AddDropStart       = DateTime.SpecifyKind(dto.AddDropStart,       DateTimeKind.Utc);
        cal.AddDropEnd         = DateTime.SpecifyKind(dto.AddDropEnd,         DateTimeKind.Utc);
        cal.WithdrawalDeadline = DateTime.SpecifyKind(dto.WithdrawalDeadline, DateTimeKind.Utc);
        cal.GradeEntryStart    = DateTime.SpecifyKind(dto.GradeEntryStart,    DateTimeKind.Utc);
        cal.GradeEntryEnd      = DateTime.SpecifyKind(dto.GradeEntryEnd,      DateTimeKind.Utc);

        if (isNew)
        {
            term.AcademicCalendar = cal;
            _ctx.TermAcademicCalendars.Add(cal);
        }

        await _ctx.SaveChangesAsync(ct);
        return ToDto(term, term.AcademicCalendar!);
    }

    public async Task UpdatePhaseOverrideAsync(int termId, UpdateCalendarPhaseOverrideDto dto, CancellationToken ct = default)
    {
        var term = await _ctx.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new KeyNotFoundException($"Term {termId} not found.");

        if (term.IsCancelled)
            throw new InvalidOperationException("İptal edilmiş dönemde override değiştirilemez.");

        var cal = term.AcademicCalendar
            ?? throw new InvalidOperationException("Bu dönem için henüz akademik takvim oluşturulmamış.");

        switch (dto.Phase)
        {
            case "Registration": cal.RegistrationOverride = dto.Value; break;
            case "AddDrop":      cal.AddDropOverride      = dto.Value; break;
            case "Withdrawal":   cal.WithdrawalOverride   = dto.Value; break;
            case "GradeEntry":   cal.GradeEntryOverride   = dto.Value; break;
            default:
                throw new ArgumentException($"Geçersiz faz: '{dto.Phase}'. " +
                    "Geçerli değerler: Registration, AddDrop, Withdrawal, GradeEntry.");
        }

        cal.UpdatedAt = _clock.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AcademicCalendarTermSummaryDto>> ListTermsAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var terms = await _ctx.AcademicTerms
            .AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .OrderByDescending(t => t.Year)
            .ThenBy(t => t.TermType)
            .ToListAsync(ct);

        return terms.Select(t => new AcademicCalendarTermSummaryDto(
            TermId:       t.Id,
            TermName:     t.Name,
            Year:         t.Year,
            TermTypeLabel: TermLabels.Get(t.TermType),
            IsActive:     t.IsActive(now),
            IsCancelled:  t.IsCancelled,
            HasCalendar:  t.AcademicCalendar is not null,
            TermStart:    t.TermStart,
            TermEnd:      t.TermEnd
        )).ToList();
    }

    // ── Public (öğrenci / hoca) ───────────────────────────────────────

    public async Task<StudentAcademicCalendarDto?> GetStudentViewAsync(int termId, CancellationToken ct = default)
    {
        var term = await _ctx.AcademicTerms
            .AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .Include(t => t.CalendarEvents.Where(e => e.IsPublic))
            .FirstOrDefaultAsync(t => t.Id == termId, ct);

        if (term is null)
            return null;

        return BuildStudentDto(term, _clock.UtcNow);
    }

    public async Task<StudentAcademicCalendarDto?> GetActiveStudentViewAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var term = await _ctx.AcademicTerms
            .AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .Include(t => t.CalendarEvents.Where(e => e.IsPublic))
            .Where(t => !t.IsCancelled && t.TermStart <= now && t.TermEnd >= now)
            .FirstOrDefaultAsync(ct);

        if (term is null)
            return null;

        return BuildStudentDto(term, now);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void Validate(UpsertAcademicCalendarDto dto, AcademicTerm term)
    {
        if (dto.RegistrationStart >= dto.RegistrationEnd)
            throw new InvalidOperationException("Kayıt başlangıç tarihi bitiş tarihinden önce olmalıdır.");

        if (dto.AddDropStart >= dto.AddDropEnd)
            throw new InvalidOperationException("Ders ekleme/bırakma başlangıç tarihi bitiş tarihinden önce olmalıdır.");

        if (dto.GradeEntryStart >= dto.GradeEntryEnd)
            throw new InvalidOperationException("Not girişi başlangıç tarihi bitiş tarihinden önce olmalıdır.");

        if (dto.RegistrationEnd > term.TermEnd)
            throw new InvalidOperationException("Kayıt bitiş tarihi dönem bitiş tarihinden sonra olamaz.");

        if (dto.AddDropEnd > term.TermEnd)
            throw new InvalidOperationException("Ders ekleme/bırakma bitiş tarihi dönem bitiş tarihinden sonra olamaz.");

        if (dto.WithdrawalDeadline > term.TermEnd)
            throw new InvalidOperationException("Çekilme tarihi dönem bitiş tarihinden sonra olamaz.");

        if (dto.WithdrawalDeadline < term.TermStart)
            throw new InvalidOperationException("Çekilme tarihi dönem başlangıç tarihinden önce olamaz.");
    }

    private AcademicCalendarDto ToDto(AcademicTerm term, TermAcademicCalendar cal)
    {
        var now = _clock.UtcNow;
        return new AcademicCalendarDto(
            Id:                  cal.Id,
            TermId:              term.Id,
            TermName:            term.Name,
            TermStart:           term.TermStart,
            TermEnd:             term.TermEnd,
            RegistrationStart:   cal.RegistrationStart,
            RegistrationEnd:     cal.RegistrationEnd,
            AddDropStart:        cal.AddDropStart,
            AddDropEnd:          cal.AddDropEnd,
            WithdrawalDeadline:  cal.WithdrawalDeadline,
            GradeEntryStart:     cal.GradeEntryStart,
            GradeEntryEnd:       cal.GradeEntryEnd,
            RegistrationOverride: cal.RegistrationOverride,
            AddDropOverride:     cal.AddDropOverride,
            WithdrawalOverride:  cal.WithdrawalOverride,
            GradeEntryOverride:  cal.GradeEntryOverride,
            IsRegistrationOpen:  term.IsRegistrationOpen(now),
            IsAddDropOpen:       term.IsAddDropOpen(now),
            IsWithdrawalOpen:    term.IsWithdrawalOpen(now),
            IsGradeEntryOpen:    term.IsGradeEntryOpen(now),
            CreatedAt:           cal.CreatedAt,
            UpdatedAt:           cal.UpdatedAt
        );
    }

    private static StudentAcademicCalendarDto BuildStudentDto(AcademicTerm term, DateTime now)
    {
        var cal = term.AcademicCalendar;

        var events = term.CalendarEvents
            .OrderBy(e => e.StartDate)
            .Select(e => new TermCalendarEventDto(
                e.Id,
                e.EventType.ToString(),
                e.Title,
                e.StartDate,
                e.EndDate,
                e.Note,
                e.IsPublic,
                e.IsGenerated,
                e.BlocksAcademicEvents
            ))
            .ToList();

        return new StudentAcademicCalendarDto(
            TermId:            term.Id,
            TermName:          term.Name,
            TermTypeLabel:     TermLabels.Get(term.TermType),
            Year:              term.Year,
            IsCancelled:       term.IsCancelled,
            TermStart:         term.TermStart,
            TermEnd:           term.TermEnd,
            RegistrationStart: cal?.RegistrationStart,
            RegistrationEnd:   cal?.RegistrationEnd,
            AddDropStart:      cal?.AddDropStart,
            AddDropEnd:        cal?.AddDropEnd,
            WithdrawalDeadline:cal?.WithdrawalDeadline,
            GradeEntryStart:   cal?.GradeEntryStart,
            GradeEntryEnd:     cal?.GradeEntryEnd,
            IsRegistrationOpen: term.IsRegistrationOpen(now),
            IsAddDropOpen:      term.IsAddDropOpen(now),
            IsWithdrawalOpen:   term.IsWithdrawalOpen(now),
            IsGradeEntryOpen:   term.IsGradeEntryOpen(now),
            Events:            events
        );
    }
}
