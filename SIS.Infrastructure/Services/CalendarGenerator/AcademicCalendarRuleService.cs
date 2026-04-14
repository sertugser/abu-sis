using Microsoft.EntityFrameworkCore;
using SIS.Application.CalendarGenerator.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.CalendarGenerator;

public class AcademicCalendarRuleService : IAcademicCalendarRuleService
{
    private const int SeventhWeekDeadlineDays = 7 * 7 - 1;
    private const int GradePublicationBusinessDayLimit = 10;

    private readonly AppDbContext _context;

    public AcademicCalendarRuleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task ValidateCalendarEventAsync(AcademicTerm term, CalendarEventType eventType, DateTime startDate, DateTime? endDate, CancellationToken ct = default)
    {
        if (term.Status >= TermStatus.Closed)
            throw new InvalidOperationException("Kapalı veya arşivlenmiş dönemde takvim etkinliği değiştirilemez.");

        var start = EnsureUtcDate(startDate);
        var end = EnsureUtcDate(endDate ?? startDate);
        if (end < start)
            throw new InvalidOperationException("Etkinlik bitis tarihi baslangic tarihinden once olamaz.");

        if (eventType is CalendarEventType.CourseWithdrawal or CalendarEventType.RegistrationFreezeDeadline
            && term.TermStart != default
            && start.Date > term.TermStart.Date.AddDays(SeventhWeekDeadlineDays))
        {
            throw new InvalidOperationException("Dersten çekilme ve kayıt dondurma son günü yedinci hafta sonunu geçemez.");
        }

        if (IsCriticalEvent(eventType))
        {
            var hasBlockingConflict = await _context.TermCalendarEvents
                .AsNoTracking()
                .AnyAsync(e => e.TermId == term.Id
                    && e.BlocksAcademicEvents
                    && start <= (e.EndDate ?? e.StartDate)
                    && e.StartDate <= end, ct);

            if (hasBlockingConflict)
                throw new InvalidOperationException("Kritik akademik etkinlik resmi tatil veya bloklayıcı önemli gün ile çakışmamalıdır.");
        }

        await ValidateGradePublicationDeadlineAsync(term.Id, eventType, start, ct);
    }

    public async Task<DateTime?> GetGradePublicationDateAsync(int termId, CancellationToken ct = default)
    {
        var publication = await _context.TermCalendarEvents
            .AsNoTracking()
            .Where(e => e.TermId == termId
                && (e.EventType == CalendarEventType.MakeupGradePublication
                    || e.EventType == CalendarEventType.FinalGradePublication
                    || e.EventType == CalendarEventType.SummerSchoolGradePublication))
            .OrderByDescending(e => e.EventType == CalendarEventType.MakeupGradePublication)
            .ThenByDescending(e => e.EventType == CalendarEventType.SummerSchoolGradePublication)
            .ThenByDescending(e => e.StartDate)
            .FirstOrDefaultAsync(ct);

        return publication?.StartDate.Date;
    }

    public async Task<bool> CanStudentSeeLetterGradeAsync(int termId, bool isFinalized, bool hasGrade, DateTime nowUtc, CancellationToken ct = default)
    {
        if (!isFinalized || !hasGrade)
            return false;

        var publicationDate = await GetGradePublicationDateAsync(termId, ct);
        return publicationDate == null || nowUtc.Date >= publicationDate.Value.Date;
    }

    public async Task ValidateTermCanCloseAsync(AcademicTerm term, CancellationToken ct = default)
    {
        var publicationDate = await GetGradePublicationDateAsync(term.Id, ct);
        if (publicationDate == null)
            throw new InvalidOperationException("Dönem kapatılmadan önce final/bütünleme/yaz okulu not ilan tarihi takvimde bulunmalıdır.");

        if (publicationDate.Value.Date > DateTime.UtcNow.Date)
            throw new InvalidOperationException("Not ilan tarihi gelmeden dönem kapatılamaz.");
    }

    private static bool IsCriticalEvent(CalendarEventType type) =>
        type is CalendarEventType.ClassesStart or CalendarEventType.ClassesEnd
            or CalendarEventType.CourseRegistration or CalendarEventType.AddDrop
            or CalendarEventType.CourseWithdrawal or CalendarEventType.RegistrationFreezeDeadline
            or CalendarEventType.MidtermExam or CalendarEventType.FinalExam
            or CalendarEventType.MakeupExam or CalendarEventType.ExcuseExam
            or CalendarEventType.MidtermGradePublication or CalendarEventType.FinalGradePublication
            or CalendarEventType.MakeupGradePublication or CalendarEventType.SummerSchoolRegistration
            or CalendarEventType.SummerSchoolExam or CalendarEventType.SummerSchoolGradePublication;

    private async Task ValidateGradePublicationDeadlineAsync(int termId, CalendarEventType eventType, DateTime publicationDate, CancellationToken ct)
    {
        var examType = eventType switch
        {
            CalendarEventType.MidtermGradePublication => CalendarEventType.MidtermExam,
            CalendarEventType.FinalGradePublication => CalendarEventType.FinalExam,
            CalendarEventType.MakeupGradePublication => CalendarEventType.MakeupExam,
            CalendarEventType.SummerSchoolGradePublication => CalendarEventType.SummerSchoolExam,
            _ => (CalendarEventType?)null
        };

        if (!examType.HasValue)
            return;

        var query = _context.TermCalendarEvents
            .AsNoTracking()
            .Where(e => e.TermId == termId && e.EventType == examType.Value);

        if (eventType == CalendarEventType.MakeupGradePublication)
            query = query.Where(e => e.TemplateItemKey == "makeup_exam" || (!e.Title.Contains("Başvuru") && !e.Title.Contains("Basvuru")));

        var exam = await query
            .OrderByDescending(e => e.EndDate ?? e.StartDate)
            .FirstOrDefaultAsync(ct);

        if (exam == null)
            return;

        var blockingDays = await _context.TermCalendarEvents
            .AsNoTracking()
            .Where(e => e.TermId == termId && e.BlocksAcademicEvents)
            .Select(e => new { e.StartDate, EndDate = e.EndDate ?? e.StartDate, e.EventType })
            .ToListAsync(ct);

        var businessDays = 0;
        var examEnd = (exam.EndDate ?? exam.StartDate).Date;
        for (var day = examEnd.AddDays(1); day <= publicationDate.Date; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            if (blockingDays.Any(h => (h.EventType is CalendarEventType.NationalHoliday or CalendarEventType.ReligiousHoliday)
                    && day >= h.StartDate.Date && day <= h.EndDate.Date))
                continue;

            businessDays++;
        }

        if (businessDays > GradePublicationBusinessDayLimit)
            throw new InvalidOperationException("Sınav sonuçları sınav dönemini izleyen en geç 10 iş günü içinde ilan edilmelidir.");
    }

    private static DateTime EnsureUtcDate(DateTime date) =>
        DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
}
