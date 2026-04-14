using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CalendarGenerator.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.CalendarGenerator;

public partial class CalendarGeneratorService
{
    private static List<GeneratedCalendarEventDto> Propagate(List<GeneratedCalendarEventDto> events, string changedEventId, DateTime oldStart, DateTime oldEnd, DateTime newStart, DateTime newEnd)
    {
        var changed = events.First(e => e.DraftEventId == changedEventId);
        var deltaDays = (newStart.Date - oldStart.Date).Days;
        if (deltaDays == 0) return events;

        return events.Select(e =>
        {
            if (e.DraftEventId == changedEventId || e.Semester != changed.Semester || e.IsHoliday || e.StartDate.Date <= oldStart.Date)
                return e;
            return e with { StartDate = e.StartDate.AddDays(deltaDays), EndDate = e.EndDate?.AddDays(deltaDays) };
        }).ToList();
    }

    private static void ValidateChronology(IReadOnlyList<GeneratedCalendarEventDto> events, List<CalendarWarningDto> warnings)
    {
        foreach (var group in events.Where(e => !e.IsHoliday).GroupBy(e => e.Semester))
        {
            var list = group.ToList();
            CheckOrder(list, group.Key, CalendarEventType.CourseRegistration, CalendarEventType.ClassesStart, warnings);
            CheckOrder(list, group.Key, CalendarEventType.ClassesStart, CalendarEventType.AddDrop, warnings);
            CheckOrder(list, group.Key, CalendarEventType.AddDrop, CalendarEventType.CourseWithdrawal, warnings);
            CheckOrder(list, group.Key, CalendarEventType.CourseWithdrawal, CalendarEventType.MidtermExam, warnings);
            CheckOrder(list, group.Key, CalendarEventType.MidtermExam, CalendarEventType.MidtermGradePublication, warnings);
            CheckOrder(list, group.Key, CalendarEventType.ClassesEnd, CalendarEventType.FinalExam, warnings);
            CheckOrder(list, group.Key, CalendarEventType.FinalExam, CalendarEventType.FinalGradePublication, warnings);
            CheckOrder(list, group.Key, CalendarEventType.FinalGradePublication, CalendarEventType.MakeupExam, warnings, secondKeyContains: "makeup_exam");
            CheckOrder(list, group.Key, CalendarEventType.MakeupExam, CalendarEventType.MakeupGradePublication, warnings, firstKeyContains: "makeup_exam");
        }
    }

    private static void CheckOrder(IReadOnlyList<GeneratedCalendarEventDto> events, SemesterType semester, CalendarEventType first, CalendarEventType second, List<CalendarWarningDto> warnings, string? firstKeyContains = null, string? secondKeyContains = null)
    {
        var firstEvent = events.Where(e => e.EventType == first && (firstKeyContains == null || (e.TemplateItemKey?.Contains(firstKeyContains, StringComparison.OrdinalIgnoreCase) ?? false)))
            .OrderBy(e => e.StartDate).LastOrDefault();
        var secondEvent = events.Where(e => e.EventType == second && (secondKeyContains == null || (e.TemplateItemKey?.Contains(secondKeyContains, StringComparison.OrdinalIgnoreCase) ?? false)))
            .OrderBy(e => e.StartDate).FirstOrDefault();
        if (firstEvent == null || secondEvent == null) return;

        if ((firstEvent.EndDate ?? firstEvent.StartDate).Date > secondEvent.StartDate.Date)
            warnings.Add(new("CHRONOLOGY", "Error", $"{semester}: {firstEvent.Title} etkinligi {secondEvent.Title} etkinliginden sonra bitiyor.", secondEvent.DraftEventId));
    }

    private static IEnumerable<CalendarWarningDto> ValidateTeachingWeekRules(IReadOnlyList<GeneratedCalendarEventDto> events, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        foreach (var group in events.Where(e => !e.IsHoliday && e.Semester != SemesterType.Summer).GroupBy(e => e.Semester))
        {
            var start = group.FirstOrDefault(e => e.EventType == CalendarEventType.ClassesStart)?.StartDate;
            var end = group.FirstOrDefault(e => e.EventType == CalendarEventType.ClassesEnd)?.StartDate;
            if (!start.HasValue || !end.HasValue) continue;

            var teachingDays = CountWeekdays(start.Value, end.Value) - CountBlockingWeekdays(holidays, start.Value, end.Value);
            if (teachingDays < 70)
                yield return new CalendarWarningDto("MIN_WEEKS", "Warning", $"{group.Key}: 14 haftalik ders suresi eksik olabilir ({teachingDays} is gunu).");
        }
    }

    private static IReadOnlyList<TermSummaryDto> BuildTermSummaries(IReadOnlyList<GeneratedCalendarEventDto> events, IReadOnlyList<ResolvedHolidayDto> holidays) =>
        events.Where(e => !e.IsHoliday).GroupBy(e => e.Semester).Select(group =>
        {
            var start = group.FirstOrDefault(e => e.EventType == CalendarEventType.ClassesStart)?.StartDate ?? group.Min(e => e.StartDate);
            var end = group.FirstOrDefault(e => e.EventType == CalendarEventType.ClassesEnd)?.StartDate ?? group.Max(e => e.EndDate ?? e.StartDate);
            var holidayDays = CountBlockingWeekdays(holidays, start, end);
            var examDays = group.Where(e => e.EventType is CalendarEventType.MidtermExam or CalendarEventType.FinalExam or CalendarEventType.MakeupExam or CalendarEventType.SummerSchoolExam)
                .Sum(e => ((e.EndDate ?? e.StartDate).Date - e.StartDate.Date).Days + 1);
            return new TermSummaryDto(group.Key, start, end, CountWeekdays(start, end) - holidayDays, holidayDays, examDays);
        }).OrderBy(s => s.Semester).ToList();

    private static void ApplyTermDates(AcademicTerm term, IReadOnlyList<GeneratedCalendarEventDto> events)
    {
        var registration = First(events, CalendarEventType.CourseRegistration) ?? First(events, CalendarEventType.SummerSchoolRegistration) ?? First(events, CalendarEventType.TuitionPayment);
        var addDrop = First(events, CalendarEventType.AddDrop) ?? registration;
        var withdrawal = First(events, CalendarEventType.CourseWithdrawal) ?? First(events, CalendarEventType.RegistrationFreezeDeadline) ?? First(events, CalendarEventType.ClassesEnd);
        var classesStart = First(events, CalendarEventType.ClassesStart) ?? events.OrderBy(e => e.StartDate).First();
        var classesEnd = First(events, CalendarEventType.ClassesEnd) ?? events.OrderBy(e => e.StartDate).Last();

        term.RegistrationStart = registration?.StartDate ?? classesStart.StartDate;
        term.RegistrationEnd = registration?.EndDate ?? registration?.StartDate ?? classesStart.StartDate;
        term.AddDropStart = addDrop?.StartDate ?? term.RegistrationStart;
        term.AddDropEnd = addDrop?.EndDate ?? addDrop?.StartDate ?? term.RegistrationEnd;
        term.WithdrawalDeadline = withdrawal?.StartDate ?? classesEnd.StartDate;
        term.TermStart = classesStart.StartDate;
        term.TermEnd = classesEnd.StartDate;
    }

    private static GeneratedCalendarEventDto? First(IReadOnlyList<GeneratedCalendarEventDto> events, CalendarEventType eventType) =>
        events.Where(e => e.EventType == eventType).OrderBy(e => e.StartDate).FirstOrDefault();

    private static AcademicTerm CreateTermShell(string academicYear, int startYear, SemesterType semester)
    {
        var label = semester switch { SemesterType.Fall => "Guz", SemesterType.Spring => "Bahar", SemesterType.Summer => "Yaz", _ => semester.ToString() };
        var english = semester switch { SemesterType.Fall => "Fall", SemesterType.Spring => "Spring", SemesterType.Summer => "Summer", _ => semester.ToString() };
        return new AcademicTerm
        {
            Name = $"{academicYear} {label}",
            ShortName = $"{startYear % 100:00}-{(startYear + 1) % 100:00} {label}",
            EnglishName = $"{english} {academicYear}",
            Year = startYear,
            Semester = semester,
            TermType = semester == SemesterType.Summer ? TermType.Summer : TermType.Regular,
            Status = TermStatus.Draft,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static CalendarDraftDto ToDraftDto(GeneratedCalendarDraft draft)
    {
        var events = Deserialize<List<GeneratedCalendarEventDto>>(draft.EventsJson);
        var holidays = Deserialize<List<ResolvedHolidayDto>>(draft.HolidaysJson);
        var warnings = Deserialize<List<CalendarWarningDto>>(draft.WarningsJson);
        return new CalendarDraftDto(draft.Id, draft.AcademicYear, draft.Status.ToString(), draft.QualityScore, draft.CreatedAt, draft.UpdatedAt, draft.ExpiresAt, events, holidays, warnings, BuildTermSummaries(events, holidays));
    }

    private async Task<GeneratedCalendarDraft> LoadDraftAsync(int draftId, CancellationToken ct) =>
        await _context.GeneratedCalendarDrafts.FirstOrDefaultAsync(d => d.Id == draftId, ct)
        ?? throw new KeyNotFoundException($"Calendar draft not found: {draftId}");

    private static void EnsureEditable(GeneratedCalendarDraft draft)
    {
        if (draft.Status != GeneratedCalendarDraftStatus.Draft)
            throw new InvalidOperationException("Sadece taslak durumundaki takvim duzenlenebilir.");
        if (draft.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Takvim taslaginin saklama suresi dolmus.");
    }

    private static int CalculateQualityScore(IReadOnlyList<CalendarWarningDto> warnings) =>
        Math.Max(0, 100 - warnings.Sum(w => w.Severity switch { "Error" => 20, "Warning" => 5, _ => 1 }));

    private static DateTime? ReadDate(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
        && DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;

    private static bool Matches(CalendarConstraintInputDto c, CalendarEventType eventType, string chainCode) =>
        (!c.TargetEventType.HasValue || c.TargetEventType.Value == eventType)
        && (string.IsNullOrWhiteSpace(c.TargetChainCode) || string.Equals(c.TargetChainCode, chainCode, StringComparison.OrdinalIgnoreCase));

    private static bool OverlapsBlockingHoliday(DateTime start, DateTime end, IReadOnlyList<ResolvedHolidayDto> holidays) =>
        holidays.Any(h => h.BlocksAcademicEvents && DateRangesOverlap(start, end, h.StartDate, h.EndDate));

    private static bool DateRangesOverlap(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
        aStart.Date <= bEnd.Date && bStart.Date <= aEnd.Date;

    private static bool StartsOnWeekend(DateTime start, CalendarEventType eventType) =>
        IsSingleDaySensitive(eventType) && start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static bool IsCriticalEvent(CalendarEventType type) =>
        type is CalendarEventType.ClassesStart or CalendarEventType.ClassesEnd or CalendarEventType.CourseRegistration
            or CalendarEventType.AddDrop or CalendarEventType.CourseWithdrawal or CalendarEventType.RegistrationFreezeDeadline
            or CalendarEventType.MidtermExam or CalendarEventType.FinalExam or CalendarEventType.MakeupExam
            or CalendarEventType.ExcuseExam or CalendarEventType.MidtermGradePublication
            or CalendarEventType.FinalGradePublication or CalendarEventType.MakeupGradePublication
            or CalendarEventType.SummerSchoolRegistration or CalendarEventType.SummerSchoolExam
            or CalendarEventType.SummerSchoolGradePublication;

    private static bool IsSingleDaySensitive(CalendarEventType type) =>
        type is CalendarEventType.ClassesStart or CalendarEventType.ClassesEnd or CalendarEventType.CourseWithdrawal
            or CalendarEventType.RegistrationFreezeDeadline or CalendarEventType.MidtermGradePublication
            or CalendarEventType.FinalGradePublication or CalendarEventType.MakeupGradePublication
            or CalendarEventType.ExcuseExamApplication or CalendarEventType.SummerSchoolGradePublication;

    private static bool BlocksAcademicEvents(CalendarEventType type) =>
        type is CalendarEventType.NationalHoliday or CalendarEventType.ReligiousHoliday or CalendarEventType.ImportantDay;

    private static int CountWeekdays(DateTime start, DateTime end)
    {
        var count = 0;
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
            if (day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) count++;
        return count;
    }

    private static int CountBlockingWeekdays(IReadOnlyList<ResolvedHolidayDto> holidays, DateTime start, DateTime end)
    {
        var days = new HashSet<DateTime>();
        foreach (var holiday in holidays.Where(h => h.BlocksAcademicEvents && DateRangesOverlap(start, end, h.StartDate, h.EndDate)))
        {
            for (var day = holiday.StartDate.Date; day <= holiday.EndDate.Date; day = day.AddDays(1))
                if (day >= start.Date && day <= end.Date && day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) days.Add(day);
        }
        return days.Count;
    }

    private static DateTime MondayOnOrAfter(DateTime date)
    {
        var current = date.Date;
        while (current.DayOfWeek != DayOfWeek.Monday) current = current.AddDays(1);
        return DateTime.SpecifyKind(current, DateTimeKind.Utc);
    }

    private static DateTime EnsureUtcDate(DateTime date) => DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static int ParseAcademicYearStart(string academicYear)
    {
        var parts = academicYear.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var startYear) || !int.TryParse(parts[1], out var endYear) || endYear != startYear + 1)
            throw new ArgumentException("Akademik yil '2025-2026' formatinda olmalidir.");
        return startYear;
    }

    private static string EventId(string academicYear, SemesterType semester, string key) => $"{academicYear}:{semester}:{key}";
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException("Calendar JSON could not be deserialized.");
}

internal static class GeneratedCalendarEventDtoOrdering
{
    public static int SortWeight(this GeneratedCalendarEventDto ev) => ev.EventType switch
    {
        CalendarEventType.NationalHoliday => 900,
        CalendarEventType.ReligiousHoliday => 901,
        CalendarEventType.ImportantDay => 902,
        _ => ev.IsHoliday ? 950 : 100
    };
}
