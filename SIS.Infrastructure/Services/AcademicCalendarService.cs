using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicCalendar.DTOs;
using SIS.Application.AcademicCalendar.Services;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AcademicCalendarService : IAcademicCalendarService
{
    private const int DependentPublicationBusinessDays = 3;
    private const int GradePublicationBusinessDayLimit = 10;
    private const int ExcuseExamMinBusinessDays = 3;
    private const int MakeupExamMinBusinessDays = 5;
    private const int MakeupAfterPublicationBufferDays = 4;

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

    public async Task<StudentAcademicCalendarDto> UpdateEventAsync(int termId, int eventId, UpdateTermCalendarEventDto dto, CancellationToken ct = default)
    {
        var term = await _ctx.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .Include(t => t.CalendarEvents)
            .FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new KeyNotFoundException($"Term {termId} not found.");

        if (term.IsCancelled)
            throw new InvalidOperationException("İptal edilmiş dönemde etkinlik taşınamaz.");

        var calendarEvent = term.CalendarEvents.FirstOrDefault(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Calendar event {eventId} not found.");

        if (!calendarEvent.IsPublic)
            throw new InvalidOperationException("Sadece akademik takvimde görünür etkinlikler taşınabilir.");

        if (IsLockedCalendarEvent(calendarEvent))
            throw new InvalidOperationException("Resmi tatiller ve bloklayıcı özel günler sürükle-bırak ile taşınamaz.");

        var startDate = EnsureUtcDate(dto.StartDate);
        var endDate = EnsureUtcDate(dto.EndDate ?? dto.StartDate);
        if (startDate > endDate)
            throw new InvalidOperationException("Başlangıç tarihi bitiş tarihinden sonra olamaz.");

        var window = CalendarEditWindow(term);
        if (startDate < window.Start || endDate > window.End)
            throw new InvalidOperationException("Etkinlik yalnizca akademik yil duzenleme araligi icinde tasinabilir.");

        if (RequiresWeekdayOnly(calendarEvent.EventType) && StartsOnWeekend(startDate))
            throw new InvalidOperationException("Sinav etkinlikleri hafta sonu baslayamaz.");

        EnsureNoBlockingOverlap(term.CalendarEvents, calendarEvent.Id, startDate, endDate);

        calendarEvent.StartDate = startDate;
        calendarEvent.EndDate = startDate == endDate ? null : endDate;

        var changedEventIds = new HashSet<int> { calendarEvent.Id };
        if (dto.PropagateChanges != false)
            ApplyDependentEventRules(term, calendarEvent, changedEventIds);

        ValidateCalendarEventGraph(term, changedEventIds);

        if (term.AcademicCalendar is not null)
        {
            SyncAcademicCalendarFromEvents(term);
            Validate(BuildUpsertDto(term.AcademicCalendar), term);
            term.AcademicCalendar.UpdatedAt = _clock.UtcNow;
        }

        await _ctx.SaveChangesAsync(ct);
        return BuildStudentDto(term, _clock.UtcNow);
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
        // ── Aynı faz içi tutarlılık ──
        if (dto.RegistrationStart >= dto.RegistrationEnd)
            throw new InvalidOperationException("Kayıt başlangıç tarihi bitiş tarihinden önce olmalıdır.");

        if (dto.AddDropStart >= dto.AddDropEnd)
            throw new InvalidOperationException("Ders ekleme/bırakma başlangıç tarihi bitiş tarihinden önce olmalıdır.");

        if (dto.GradeEntryStart >= dto.GradeEntryEnd)
            throw new InvalidOperationException("Not girişi başlangıç tarihi bitiş tarihinden önce olmalıdır.");

        // ── Dönem sınırı ──
        if (dto.RegistrationEnd > term.TermEnd)
            throw new InvalidOperationException("Kayıt bitiş tarihi dönem bitiş tarihinden sonra olamaz.");

        if (dto.AddDropEnd > term.TermEnd)
            throw new InvalidOperationException("Ders ekleme/bırakma bitiş tarihi dönem bitiş tarihinden sonra olamaz.");

        if (dto.WithdrawalDeadline > term.TermEnd)
            throw new InvalidOperationException("Çekilme tarihi dönem bitiş tarihinden sonra olamaz.");

        if (dto.WithdrawalDeadline < term.TermStart)
            throw new InvalidOperationException("Çekilme tarihi dönem başlangıç tarihinden önce olamaz.");

        // ── Faz sıralaması (yeni kurallar — mantıksal çelişkiyi engelle) ──
        if (dto.RegistrationEnd > dto.AddDropStart)
            throw new InvalidOperationException(
                "Ekle-bırak, kayıt bitmeden başlayamaz. Kayıt bitişini ekle-bırak başlangıcından önce olacak şekilde ayarlayın.");

        if (dto.AddDropEnd > dto.GradeEntryStart)
            throw new InvalidOperationException(
                "Not girişi, ekle-bırak bitmeden başlayamaz. Ekle-bırak bitişini not girişi başlangıcından önce olacak şekilde ayarlayın.");

        if (dto.GradeEntryStart < term.TermStart)
            throw new InvalidOperationException(
                "Not girişi, dönem başlamadan başlayamaz. Not girişi başlangıcını dönem başlangıcına veya sonrasına ayarlayın.");
    }

    private static UpsertAcademicCalendarDto BuildUpsertDto(TermAcademicCalendar calendar) =>
        new(
            calendar.RegistrationStart,
            calendar.RegistrationEnd,
            calendar.AddDropStart,
            calendar.AddDropEnd,
            calendar.WithdrawalDeadline,
            calendar.GradeEntryStart,
            calendar.GradeEntryEnd);

    private static void SyncAcademicCalendarFromEvents(AcademicTerm term)
    {
        var calendar = term.AcademicCalendar;
        if (calendar is null)
            return;

        var registration = FirstEvent(term.CalendarEvents,
            CalendarEventType.CourseRegistration,
            CalendarEventType.NewStudentRegistration,
            CalendarEventType.GraduateRegistration,
            CalendarEventType.TransferRegistration,
            CalendarEventType.SummerSchoolRegistration);
        var addDrop = FirstEvent(term.CalendarEvents, CalendarEventType.AddDrop, CalendarEventType.AddDropApproval);
        var withdrawal = FirstEvent(term.CalendarEvents, CalendarEventType.CourseWithdrawal, CalendarEventType.AdvisorWithdrawal);
        var gradeEntryStart = FirstEvent(term.CalendarEvents,
            CalendarEventType.MidtermGradeEntry,
            CalendarEventType.FinalGradeEntry,
            CalendarEventType.MakeupGradeEntry,
            CalendarEventType.MidtermGradePublication,
            CalendarEventType.FinalGradePublication,
            CalendarEventType.MakeupGradePublication,
            CalendarEventType.SummerSchoolGradePublication);
        var gradeEntryEnd = LastEvent(term.CalendarEvents,
            CalendarEventType.MakeupGradeEntry,
            CalendarEventType.FinalGradeEntry,
            CalendarEventType.MakeupGradePublication,
            CalendarEventType.FinalGradePublication,
            CalendarEventType.SummerSchoolGradePublication,
            CalendarEventType.MidtermGradeEntry,
            CalendarEventType.MidtermGradePublication);

        calendar.RegistrationStart = registration?.StartDate ?? calendar.RegistrationStart;
        calendar.RegistrationEnd = registration?.EndDate ?? registration?.StartDate ?? calendar.RegistrationEnd;
        calendar.AddDropStart = addDrop?.StartDate ?? calendar.AddDropStart;
        calendar.AddDropEnd = addDrop?.EndDate ?? addDrop?.StartDate ?? calendar.AddDropEnd;
        calendar.WithdrawalDeadline = withdrawal?.EndDate ?? withdrawal?.StartDate ?? calendar.WithdrawalDeadline;
        calendar.GradeEntryStart = gradeEntryStart?.StartDate ?? calendar.GradeEntryStart;
        calendar.GradeEntryEnd = gradeEntryEnd?.EndDate ?? gradeEntryEnd?.StartDate ?? calendar.GradeEntryEnd;
    }

    private static TermCalendarEvent? FirstEvent(IEnumerable<TermCalendarEvent> events, params CalendarEventType[] eventTypes) =>
        events
            .Where(e => e.IsPublic && eventTypes.Contains(e.EventType))
            .OrderBy(e => e.StartDate)
            .FirstOrDefault();

    private static TermCalendarEvent? LastEvent(IEnumerable<TermCalendarEvent> events, params CalendarEventType[] eventTypes) =>
        events
            .Where(e => e.IsPublic && eventTypes.Contains(e.EventType))
            .OrderByDescending(e => e.EndDate ?? e.StartDate)
            .FirstOrDefault();

    private static DateTime EnsureUtcDate(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).Date;

    private static bool IsLockedCalendarEvent(TermCalendarEvent calendarEvent) =>
        calendarEvent.BlocksAcademicEvents
        || calendarEvent.EventType is CalendarEventType.NationalHoliday or CalendarEventType.ReligiousHoliday or CalendarEventType.ImportantDay;

    private static bool RequiresWeekdayOnly(CalendarEventType eventType) =>
        eventType is CalendarEventType.MidtermExam
            or CalendarEventType.FinalExam
            or CalendarEventType.MakeupExam
            or CalendarEventType.ExcuseExam
            or CalendarEventType.PrepSchoolExam
            or CalendarEventType.SummerSchoolExam;

    private static bool ContainsWeekend(DateTime startDate, DateTime endDate)
    {
        for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                return true;
        }

        return false;
    }

    private static void EnsureNoBlockingOverlap(IEnumerable<TermCalendarEvent> events, int movingEventId, DateTime startDate, DateTime endDate)
    {
        var blockingEvent = events
            .Where(e => e.Id != movingEventId && e.BlocksAcademicEvents)
            .FirstOrDefault(e => RangesOverlap(startDate, endDate, e.StartDate.Date, (e.EndDate ?? e.StartDate).Date));

        if (blockingEvent is null)
            return;

        throw new InvalidOperationException($"Seçilen tarih aralığı {blockingEvent.Title} etkinliği ile çakışıyor.");
    }

    private static bool RangesOverlap(DateTime leftStart, DateTime leftEnd, DateTime rightStart, DateTime rightEnd) =>
        leftStart <= rightEnd && rightStart <= leftEnd;

    private static void ApplyDependentEventRules(AcademicTerm term, TermCalendarEvent movedEvent, ISet<int> changedEventIds)
    {
        ApplyMidtermDependencies(term, movedEvent, changedEventIds);
        ApplyFinalAndMakeupDependencies(term, movedEvent, changedEventIds);
        ApplySummerDependencies(term, movedEvent, changedEventIds);
    }

    private static void ApplyMidtermDependencies(AcademicTerm term, TermCalendarEvent movedEvent, ISet<int> changedEventIds)
    {
        var movedMidterm = IsMidtermExamEvent(movedEvent);
        var movedExcuseExam = IsMidtermExcuseExamEvent(movedEvent);
        if (!movedMidterm && !movedExcuseExam)
            return;

        var midterm = FindMidtermExam(term);
        if (midterm is null)
            return;

        var midtermEnd = EventEnd(midterm);

        if (movedMidterm)
        {
            var excuseApplication = FindMidtermExcuseApplication(term);
            if (excuseApplication is not null)
                SetSingleDay(excuseApplication, NextBusinessDayAfter(midtermEnd, term.CalendarEvents), changedEventIds);

            var excuseExam = FindMidtermExcuseExam(term);
            if (excuseExam is not null)
                SetPreservingSpan(excuseExam, NthBusinessDayAfter(midtermEnd, ExcuseExamMinBusinessDays, term.CalendarEvents), changedEventIds);
        }

        var refreshedExcuseExam = FindMidtermExcuseExam(term);
        var publication = FindEventByKeyOrType(term, "midterm_publication", CalendarEventType.MidtermGradePublication);
        if (refreshedExcuseExam is null || publication is null)
            return;

        var excuseEnd = EventEnd(refreshedExcuseExam);
        var earliestPublication = NextBusinessDayAfter(excuseEnd, term.CalendarEvents);
        var preferredPublication = NthBusinessDayAfter(excuseEnd, DependentPublicationBusinessDays, term.CalendarEvents);
        var latestAllowedPublication = NthBusinessDayAfter(midtermEnd, GradePublicationBusinessDayLimit, term.CalendarEvents);
        var desiredPublication = preferredPublication.Date > latestAllowedPublication.Date
            ? (latestAllowedPublication.Date >= earliestPublication.Date ? latestAllowedPublication : earliestPublication)
            : preferredPublication;

        SetSingleDay(publication, desiredPublication, changedEventIds);
    }

    private static void ApplyFinalAndMakeupDependencies(AcademicTerm term, TermCalendarEvent movedEvent, ISet<int> changedEventIds)
    {
        var movedFinal = MatchesKey(movedEvent, "final_exam") || movedEvent.EventType == CalendarEventType.FinalExam;
        var movedFinalPublication = MatchesKey(movedEvent, "final_publication") || movedEvent.EventType == CalendarEventType.FinalGradePublication;
        var movedMakeup = IsMakeupExamEvent(movedEvent);
        if (!movedFinal && !movedFinalPublication && !movedMakeup)
            return;

        var finalExam = FindEventByKeyOrType(term, "final_exam", CalendarEventType.FinalExam);
        if (finalExam is null)
            return;

        var finalEnd = EventEnd(finalExam);
        var finalPublication = FindEventByKeyOrType(term, "final_publication", CalendarEventType.FinalGradePublication);
        if (movedFinal && finalPublication is not null)
            SetSingleDay(finalPublication, NthBusinessDayAfter(finalEnd, DependentPublicationBusinessDays, term.CalendarEvents), changedEventIds);

        finalPublication = FindEventByKeyOrType(term, "final_publication", CalendarEventType.FinalGradePublication);
        var makeupExam = FindMakeupExam(term);
        if (makeupExam is null)
            return;

        if (movedFinal || movedFinalPublication)
        {
            var fifthBusinessDay = NthBusinessDayAfter(finalEnd, MakeupExamMinBusinessDays, term.CalendarEvents);
            var publicationBuffer = (finalPublication?.StartDate ?? finalEnd).AddDays(MakeupAfterPublicationBufferDays);
            SetPreservingSpan(makeupExam, AlignMakeupStart(MaxDate(fifthBusinessDay, publicationBuffer), term.CalendarEvents), changedEventIds);
        }

        var makeupApplication = FindMakeupApplication(term);
        if (makeupApplication is not null)
            SetSingleDay(makeupApplication, PreviousBusinessDayBefore(makeupExam.StartDate, term.CalendarEvents), changedEventIds);

        var makeupPublication = FindEventByKeyOrType(term, "makeup_publication", CalendarEventType.MakeupGradePublication);
        if (makeupPublication is not null)
            SetSingleDay(makeupPublication, NthBusinessDayAfter(EventEnd(makeupExam), DependentPublicationBusinessDays, term.CalendarEvents), changedEventIds);
    }

    private static void ApplySummerDependencies(AcademicTerm term, TermCalendarEvent movedEvent, ISet<int> changedEventIds)
    {
        var movedSummerExam = MatchesKey(movedEvent, "summer_exam") || movedEvent.EventType == CalendarEventType.SummerSchoolExam;
        var movedSummerExcuse = MatchesKey(movedEvent, "summer_excuse_exam")
            || (movedEvent.EventType == CalendarEventType.ExcuseExam && string.Equals(movedEvent.ChainCode, "SUMMER", StringComparison.OrdinalIgnoreCase));
        if (!movedSummerExam && !movedSummerExcuse)
            return;

        var summerExam = FindEventByKeyOrType(term, "summer_exam", CalendarEventType.SummerSchoolExam);
        if (summerExam is null)
            return;

        if (movedSummerExam)
        {
            var application = FindEventByKeyOrType(term, "summer_excuse_application", CalendarEventType.ExcuseExamApplication);
            if (application is not null)
                SetSingleDay(application, NextBusinessDayAfter(EventEnd(summerExam), term.CalendarEvents), changedEventIds);

            var excuseExam = FindEventByKeyOrType(term, "summer_excuse_exam", CalendarEventType.ExcuseExam);
            if (excuseExam is not null)
                SetPreservingSpan(excuseExam, NthBusinessDayAfter(EventEnd(summerExam), ExcuseExamMinBusinessDays, term.CalendarEvents), changedEventIds);
        }

        var refreshedExcuse = FindEventByKeyOrType(term, "summer_excuse_exam", CalendarEventType.ExcuseExam);
        var publication = FindEventByKeyOrType(term, "summer_publication", CalendarEventType.SummerSchoolGradePublication);
        if (publication is not null)
            SetSingleDay(publication, NthBusinessDayAfter(EventEnd(refreshedExcuse ?? summerExam), DependentPublicationBusinessDays, term.CalendarEvents), changedEventIds);
    }

    private static void ValidateCalendarEventGraph(AcademicTerm term, IReadOnlySet<int> changedEventIds)
    {
        foreach (var calendarEvent in term.CalendarEvents.Where(e => e.IsPublic && !IsLockedCalendarEvent(e) && changedEventIds.Contains(e.Id)))
        {
            var startDate = calendarEvent.StartDate.Date;
            var endDate = EventEnd(calendarEvent);

            if (startDate > endDate)
                throw new InvalidOperationException($"{calendarEvent.Title} icin baslangic tarihi bitis tarihinden sonra olamaz.");

            var window = CalendarEditWindow(term);
            if (startDate < window.Start || endDate > window.End)
                throw new InvalidOperationException($"{calendarEvent.Title} yalnizca akademik yil duzenleme araligi icinde tasinabilir.");

            if (RequiresWeekdayOnly(calendarEvent.EventType) && StartsOnWeekend(startDate))
                throw new InvalidOperationException("Sinav etkinlikleri hafta sonu baslayamaz.");

            EnsureNoBlockingOverlap(term.CalendarEvents, calendarEvent.Id, startDate, endDate);
        }

        EnsureEventOrder(FindEventByKeyOrType(term, "classes_start", CalendarEventType.ClassesStart),
            FindEventByKeyOrType(term, "classes_end", CalendarEventType.ClassesEnd),
            "Derslerin baslangici derslerin bitisinden sonra olamaz.");

        EnsureEventOrder(FindMidtermExam(term),
            FindEventByKeyOrType(term, "final_exam", CalendarEventType.FinalExam),
            "Ara sinavlar donem sonu sinavlarindan sonra olamaz.");

        var midterm = FindMidtermExam(term);
        var midtermExcuse = FindMidtermExcuseExam(term);
        var midtermPublication = FindEventByKeyOrType(term, "midterm_publication", CalendarEventType.MidtermGradePublication);
        EnsureEventOrder(midterm, midtermExcuse, "Mazeret sinavi ara sinavlardan once olamaz.");
        EnsureEventOrder(midtermExcuse, midtermPublication, "Ara sinav ilan tarihi mazeret sinavindan once olamaz.");
        if (midterm is not null && midtermPublication is not null)
        {
            var businessDays = CountBusinessDaysAfter(EventEnd(midterm), midtermPublication.StartDate, term.CalendarEvents);
            if (businessDays > GradePublicationBusinessDayLimit)
                throw new InvalidOperationException($"Ara sinav ilan tarihi sinav donemini izleyen en gec {GradePublicationBusinessDayLimit} is gunu icinde olmalidir.");
        }

        var finalExam = FindEventByKeyOrType(term, "final_exam", CalendarEventType.FinalExam);
        var finalPublication = FindEventByKeyOrType(term, "final_publication", CalendarEventType.FinalGradePublication);
        var makeupExam = FindMakeupExam(term);
        var makeupPublication = FindEventByKeyOrType(term, "makeup_publication", CalendarEventType.MakeupGradePublication);
        EnsureEventOrder(finalExam, finalPublication, "Final ilan tarihi final sinavindan once olamaz.");
        EnsureEventOrder(finalPublication, makeupExam, "Butunleme sinavi final ilanindan once olamaz.");
        EnsureEventOrder(makeupExam, makeupPublication, "Butunleme ilan tarihi butunleme sinavindan once olamaz.");
    }

    private static void EnsureEventOrder(TermCalendarEvent? first, TermCalendarEvent? second, string message)
    {
        if (first is null || second is null)
            return;

        if (EventEnd(first).Date > second.StartDate.Date)
            throw new InvalidOperationException(message);
    }

    private static TermCalendarEvent? FindMidtermExam(AcademicTerm term) =>
        term.CalendarEvents
            .Where(e => e.IsPublic)
            .OrderBy(e => e.StartDate)
            .FirstOrDefault(IsMidtermExamEvent);

    private static TermCalendarEvent? FindMidtermExcuseApplication(AcademicTerm term) =>
        FindEventByKeyOrType(term, "midterm_excuse_application", CalendarEventType.ExcuseExamApplication, "Mazeret", "Basvuru", "Başvuru");

    private static TermCalendarEvent? FindMidtermExcuseExam(AcademicTerm term) =>
        term.CalendarEvents
            .Where(e => e.IsPublic)
            .OrderBy(e => e.StartDate)
            .FirstOrDefault(IsMidtermExcuseExamEvent);

    private static TermCalendarEvent? FindMakeupApplication(AcademicTerm term) =>
        FindEventByKeyOrType(term, "makeup_application", CalendarEventType.MakeupExamApplication, "Basvuru", "Başvuru")
        ?? term.CalendarEvents
            .Where(e => e.IsPublic)
            .OrderBy(e => e.StartDate)
            .FirstOrDefault(e => e.EventType == CalendarEventType.MakeupExam && (e.EndDate is null || TitleContainsAny(e, "Basvuru", "Başvuru")));

    private static TermCalendarEvent? FindMakeupExam(AcademicTerm term) =>
        term.CalendarEvents
            .Where(e => e.IsPublic)
            .OrderBy(e => e.StartDate)
            .FirstOrDefault(IsMakeupExamEvent);

    private static TermCalendarEvent? FindEventByKeyOrType(AcademicTerm term, string templateItemKey, CalendarEventType eventType, params string[] titleFragments)
    {
        var events = term.CalendarEvents.Where(e => e.IsPublic).OrderBy(e => e.StartDate).ToList();
        return events.FirstOrDefault(e => MatchesKey(e, templateItemKey))
            ?? events.FirstOrDefault(e => e.EventType == eventType && (titleFragments.Length == 0 || TitleContainsAny(e, titleFragments)));
    }

    private static bool IsMidtermExamEvent(TermCalendarEvent calendarEvent) =>
        MatchesKey(calendarEvent, "midterm_exam")
        || (calendarEvent.EventType == CalendarEventType.MidtermExam && !TitleContainsAny(calendarEvent, "Mazeret"));

    private static bool IsMidtermExcuseExamEvent(TermCalendarEvent calendarEvent) =>
        MatchesKey(calendarEvent, "midterm_excuse_exam")
        || calendarEvent.EventType == CalendarEventType.ExcuseExam && !string.Equals(calendarEvent.ChainCode, "SUMMER", StringComparison.OrdinalIgnoreCase)
        || calendarEvent.EventType == CalendarEventType.MidtermExam && TitleContainsAny(calendarEvent, "Mazeret");

    private static bool IsMakeupExamEvent(TermCalendarEvent calendarEvent) =>
        MatchesKey(calendarEvent, "makeup_exam")
        || (calendarEvent.EventType == CalendarEventType.MakeupExam && calendarEvent.EndDate is not null && !TitleContainsAny(calendarEvent, "Basvuru", "Başvuru"));

    private static bool MatchesKey(TermCalendarEvent calendarEvent, string templateItemKey) =>
        string.Equals(calendarEvent.TemplateItemKey, templateItemKey, StringComparison.OrdinalIgnoreCase);

    private static bool TitleContainsAny(TermCalendarEvent calendarEvent, params string[] fragments) =>
        fragments.Any(fragment => calendarEvent.Title.Contains(fragment, StringComparison.CurrentCultureIgnoreCase));

    private static DateTime EventEnd(TermCalendarEvent calendarEvent) =>
        (calendarEvent.EndDate ?? calendarEvent.StartDate).Date;

    private static void SetSingleDay(TermCalendarEvent calendarEvent, DateTime date, ISet<int> changedEventIds)
    {
        changedEventIds.Add(calendarEvent.Id);
        calendarEvent.StartDate = EnsureUtcDate(date);
        calendarEvent.EndDate = null;
    }

    private static void SetPreservingSpan(TermCalendarEvent calendarEvent, DateTime startDate, ISet<int> changedEventIds)
    {
        changedEventIds.Add(calendarEvent.Id);
        var spanDays = (EventEnd(calendarEvent) - calendarEvent.StartDate.Date).Days;
        var endDate = startDate.Date.AddDays(spanDays);
        calendarEvent.StartDate = EnsureUtcDate(startDate);
        calendarEvent.EndDate = endDate.Date == startDate.Date ? null : EnsureUtcDate(endDate);
    }

    private static DateTime MaxDate(DateTime left, DateTime right) =>
        left.Date >= right.Date ? left.Date : right.Date;

    private static (DateTime Start, DateTime End) CalendarEditWindow(AcademicTerm term) =>
        (EnsureUtcDate(new DateTime(term.Year, 6, 1)), EnsureUtcDate(new DateTime(term.Year + 1, 9, 30)));

    private static bool StartsOnWeekend(DateTime date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static bool IsBusinessDay(DateTime date, IEnumerable<TermCalendarEvent> events)
    {
        if (StartsOnWeekend(date))
            return false;

        return !events.Any(e => e.BlocksAcademicEvents && RangesOverlap(date.Date, date.Date, e.StartDate.Date, EventEnd(e)));
    }

    private static DateTime NextBusinessDayAfter(DateTime fromExclusive, IEnumerable<TermCalendarEvent> events) =>
        NthBusinessDayAfter(fromExclusive, 1, events);

    private static DateTime NthBusinessDayAfter(DateTime fromExclusive, int businessDayCount, IEnumerable<TermCalendarEvent> events)
    {
        var count = 0;
        var day = fromExclusive.Date;
        var eventList = events.ToList();
        while (count < businessDayCount)
        {
            day = day.AddDays(1);
            if (!IsBusinessDay(day, eventList))
                continue;

            count++;
        }

        return EnsureUtcDate(day);
    }

    private static DateTime PreviousBusinessDayBefore(DateTime date, IEnumerable<TermCalendarEvent> events)
    {
        var eventList = events.ToList();
        var day = date.Date.AddDays(-1);
        while (!IsBusinessDay(day, eventList))
            day = day.AddDays(-1);

        return EnsureUtcDate(day);
    }

    private static DateTime AlignMakeupStart(DateTime candidate, IEnumerable<TermCalendarEvent> events)
    {
        var eventList = events.ToList();
        var current = candidate.Date;
        while (!IsBusinessDay(current, eventList))
            current = current.AddDays(1);

        if (current.DayOfWeek == DayOfWeek.Friday)
            current = current.AddDays(3);

        return EnsureUtcDate(current);
    }

    private static int CountBusinessDaysAfter(DateTime fromExclusive, DateTime toInclusive, IEnumerable<TermCalendarEvent> events)
    {
        var count = 0;
        var eventList = events.ToList();
        for (var day = fromExclusive.Date.AddDays(1); day <= toInclusive.Date; day = day.AddDays(1))
        {
            if (IsBusinessDay(day, eventList))
                count++;
        }

        return count;
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
                e.BlocksAcademicEvents,
                e.ChainCode,
                e.TemplateItemKey
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
