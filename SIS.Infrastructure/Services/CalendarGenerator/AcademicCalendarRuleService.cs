using Microsoft.EntityFrameworkCore;
using SIS.Application.CalendarGenerator.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.CalendarGenerator;

public class AcademicCalendarRuleService : IAcademicCalendarRuleService
{
    private readonly AppDbContext _context;

    public AcademicCalendarRuleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task ValidateCalendarEventAsync(AcademicTerm term, CalendarEventType eventType, DateTime startDate, DateTime? endDate, CancellationToken ct = default)
    {
        if (term.Status >= TermStatus.Closed)
            throw new InvalidOperationException("Kapali veya arsivlenmis donemde takvim etkinligi degistirilemez.");

        var start = EnsureUtcDate(startDate);
        var end = EnsureUtcDate(endDate ?? startDate);
        if (end < start)
            throw new InvalidOperationException("Etkinlik bitis tarihi baslangic tarihinden once olamaz.");

        if (term.Status == TermStatus.Closed
            && eventType is CalendarEventType.FinalGradePublication or CalendarEventType.MakeupGradePublication
            && start.Date > DateTime.UtcNow.Date)
        {
            throw new InvalidOperationException("Kapanmis donem icin ileri tarihli harf notu ilani yapilamaz.");
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
                throw new InvalidOperationException("Kritik akademik etkinlik resmi tatil veya bloklayici onemli gun ile cakismamalidir.");
        }
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
            throw new InvalidOperationException("Donem kapatilmadan once final/butunleme/yaz okulu not ilan tarihi takvimde bulunmalidir.");

        if (publicationDate.Value.Date > DateTime.UtcNow.Date)
            throw new InvalidOperationException("Not ilan tarihi gelmeden donem kapatilamaz.");
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

    private static DateTime EnsureUtcDate(DateTime date) =>
        DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
}
