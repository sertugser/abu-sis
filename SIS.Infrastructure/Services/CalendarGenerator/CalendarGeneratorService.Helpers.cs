using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CalendarGenerator.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.CalendarGenerator;

public partial class CalendarGeneratorService
{
    private const int RegularTeachingPeriodDays = 14 * 7;
    private const int RegularSemesterWithExamsDays = 16 * 7;
    private const int SummerTeachingWeekdays = 7 * 5;
    private const int SeventhWeekDeadlineDays = 7 * 7 - 1;
    private const int GradePublicationBusinessDayLimit = 10;

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
            warnings.Add(new("CHRONOLOGY", "Error", $"{SemesterLabel(semester)}: {firstEvent.Title} etkinliği {secondEvent.Title} etkinliğinden sonra bitiyor.", secondEvent.DraftEventId));
    }

    private static IEnumerable<CalendarWarningDto> ValidateRegulationRules(IReadOnlyList<GeneratedCalendarEventDto> events, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        foreach (var group in events.Where(e => !e.IsHoliday).GroupBy(e => e.Semester))
        {
            var list = group.ToList();
            foreach (var warning in group.Key == SemesterType.Summer
                ? ValidateSummerRegulation(list, holidays)
                : ValidateRegularSemesterRegulation(group.Key, list, holidays))
            {
                yield return warning;
            }
        }
    }

    private static IEnumerable<CalendarWarningDto> ValidateRegularSemesterRegulation(SemesterType semester, IReadOnlyList<GeneratedCalendarEventDto> events, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var classesStart = First(events, CalendarEventType.ClassesStart);
        var classesEnd = First(events, CalendarEventType.ClassesEnd);
        var finalExam = FirstByTemplateKey(events, "final_exam") ?? First(events, CalendarEventType.FinalExam);

        if (classesStart == null || classesEnd == null || finalExam == null)
        {
            yield return new CalendarWarningDto("REG_REQUIRED_EVENT", "Error", $"{SemesterLabel(semester)}: Yönetmelik kontrolü için ders başlangıcı, ders bitişi ve final sınavı bulunmalıdır.");
            yield break;
        }

        var classesEndDate = classesEnd.EndDate ?? classesEnd.StartDate;
        var teachingPeriodDays = InclusiveDays(classesStart.StartDate, classesEndDate);
        if (teachingPeriodDays < RegularTeachingPeriodDays)
        {
            yield return new CalendarWarningDto(
                "REG_MIN_TEACHING_WEEKS",
                "Error",
                $"{SemesterLabel(semester)}: Madde 21 gereği kayıt ve sınavlar hariç ders süresi en az 14 hafta olmalıdır ({teachingPeriodDays} gün).",
                classesEnd.DraftEventId);
        }

        var semesterWithExamsDays = InclusiveDays(classesStart.StartDate, finalExam.EndDate ?? finalExam.StartDate);
        if (semesterWithExamsDays < RegularSemesterWithExamsDays)
        {
            yield return new CalendarWarningDto(
                "REG_MIN_SEMESTER_WEEKS",
                "Error",
                $"{SemesterLabel(semester)}: Madde 7 gereği yarıyıl sınavlar dahil en az 16 hafta olmalıdır ({semesterWithExamsDays} gün).",
                finalExam.DraftEventId);
        }

        foreach (var deadlineType in new[] { CalendarEventType.CourseWithdrawal, CalendarEventType.RegistrationFreezeDeadline })
        {
            var deadline = First(events, deadlineType);
            if (deadline == null) continue;

            var lastAllowed = classesStart.StartDate.AddDays(SeventhWeekDeadlineDays);
            if (deadline.StartDate.Date > lastAllowed.Date)
            {
                yield return new CalendarWarningDto(
                    deadlineType == CalendarEventType.CourseWithdrawal ? "REG_WITHDRAWAL_DEADLINE" : "REG_FREEZE_DEADLINE",
                    "Error",
                    $"{SemesterLabel(semester)}: Madde {(deadlineType == CalendarEventType.CourseWithdrawal ? 30 : 51)} gereği son gün yedinci hafta sonunu geçemez.",
                    deadline.DraftEventId);
            }
        }

        foreach (var warning in ValidatePublicationDeadline(events, holidays, CalendarEventType.MidtermExam, "midterm_exam", CalendarEventType.MidtermGradePublication, "midterm_publication", semester))
            yield return warning;
        foreach (var warning in ValidatePublicationDeadline(events, holidays, CalendarEventType.FinalExam, "final_exam", CalendarEventType.FinalGradePublication, "final_publication", semester))
            yield return warning;
        foreach (var warning in ValidatePublicationDeadline(events, holidays, CalendarEventType.MakeupExam, "makeup_exam", CalendarEventType.MakeupGradePublication, "makeup_publication", semester))
            yield return warning;
    }

    private static IEnumerable<CalendarWarningDto> ValidateSummerRegulation(IReadOnlyList<GeneratedCalendarEventDto> events, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var classesStart = First(events, CalendarEventType.ClassesStart);
        var classesEnd = First(events, CalendarEventType.ClassesEnd);
        var summerExam = First(events, CalendarEventType.SummerSchoolExam);

        if (classesStart == null || classesEnd == null || summerExam == null)
        {
            yield return new CalendarWarningDto("REG_SUMMER_REQUIRED_EVENT", "Error", "Yaz: Yönetmelik kontrolü için ders başlangıcı, ders bitişi ve yaz okulu sınavı bulunmalıdır.");
            yield break;
        }

        var classesEndDate = classesEnd.EndDate ?? classesEnd.StartDate;
        var teachingWeekdays = CountWeekdays(classesStart.StartDate, classesEndDate);
        if (teachingWeekdays < SummerTeachingWeekdays)
        {
            yield return new CalendarWarningDto(
                "REG_SUMMER_TEACHING_WEEKS",
                "Error",
                $"Yaz: Madde 22 gereği yaz okulu sınavlar hariç 7 haftalık ders süresini kapsamalıdır ({teachingWeekdays} hafta içi gün).",
                classesEnd.DraftEventId);
        }

        if (summerExam.StartDate.Date <= classesEndDate.Date)
        {
            yield return new CalendarWarningDto(
                "REG_SUMMER_EXAM_ORDER",
                "Error",
                "Yaz: Madde 22 gereği yaz okulu sınavları ders süresi tamamlandıktan sonra başlamalıdır.",
                summerExam.DraftEventId);
        }

        foreach (var warning in ValidatePublicationDeadline(events, holidays, CalendarEventType.SummerSchoolExam, "summer_exam", CalendarEventType.SummerSchoolGradePublication, "summer_publication", SemesterType.Summer))
            yield return warning;
    }

    private static IEnumerable<CalendarWarningDto> ValidatePublicationDeadline(
        IReadOnlyList<GeneratedCalendarEventDto> events,
        IReadOnlyList<ResolvedHolidayDto> holidays,
        CalendarEventType examType,
        string examKey,
        CalendarEventType publicationType,
        string publicationKey,
        SemesterType semester)
    {
        var exam = FirstByTemplateKey(events, examKey) ?? First(events, examType);
        var publication = FirstByTemplateKey(events, publicationKey) ?? First(events, publicationType);
        if (exam == null || publication == null)
            yield break;

        var examEnd = exam.EndDate ?? exam.StartDate;
        if (publication.StartDate.Date < examEnd.Date)
            yield break;

        var businessDays = CountBusinessDaysAfter(examEnd, publication.StartDate, holidays);
        if (businessDays > GradePublicationBusinessDayLimit)
        {
            yield return new CalendarWarningDto(
                "REG_GRADE_PUBLICATION",
                "Error",
                $"{SemesterLabel(semester)}: Madde 36 gereği {publication.Title} sınav dönemini izleyen en geç 10 iş günü içinde ilan edilmelidir ({businessDays} iş günü).",
                publication.DraftEventId);
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

    private static GeneratedCalendarEventDto? FirstByTemplateKey(IReadOnlyList<GeneratedCalendarEventDto> events, string templateKey) =>
        events.Where(e => string.Equals(e.TemplateItemKey, templateKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.StartDate)
            .FirstOrDefault();

    private static AcademicTerm CreateTermShell(string academicYear, int startYear, SemesterType semester)
    {
        var label = SemesterLabel(semester);
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

    private static string SemesterLabel(SemesterType semester) =>
        semester switch
        {
            SemesterType.Fall => "Güz",
            SemesterType.Spring => "Bahar",
            SemesterType.Summer => "Yaz",
            _ => semester.ToString()
        };

    private static CalendarDraftDto ToDraftDto(GeneratedCalendarDraft draft)
    {
        var events = Deserialize<List<GeneratedCalendarEventDto>>(draft.EventsJson);
        var holidays = Deserialize<List<ResolvedHolidayDto>>(draft.HolidaysJson);
        var warnings = Deserialize<List<CalendarWarningDto>>(draft.WarningsJson);
        return new CalendarDraftDto(draft.Id, draft.AcademicYear, draft.Status.ToString(), draft.QualityScore, draft.CreatedAt, draft.UpdatedAt, draft.ExpiresAt, events, holidays, warnings, BuildTermSummaries(events, holidays));
    }

    private static CalendarDraftSummaryDto ToDraftSummaryDto(GeneratedCalendarDraft draft) =>
        new(
            draft.Id,
            draft.AcademicYear,
            draft.Status.ToString(),
            draft.QualityScore,
            draft.CreatedAt,
            draft.UpdatedAt,
            draft.ExpiresAt,
            draft.AppliedAt,
            CountSerializedItems<GeneratedCalendarEventDto>(draft.EventsJson),
            CountSerializedItems<CalendarWarningDto>(draft.WarningsJson));

    private static int CountSerializedItems<T>(string json)
    {
        try
        {
            return Deserialize<List<T>>(json).Count;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private async Task<GeneratedCalendarDraft> LoadDraftAsync(int draftId, CancellationToken ct) =>
        await _context.GeneratedCalendarDrafts.FirstOrDefaultAsync(d => d.Id == draftId, ct)
        ?? throw new KeyNotFoundException($"Calendar draft not found: {draftId}");

    private async Task ResetDraftNumberingAsync(CancellationToken ct)
    {
        var provider = _context.Database.ProviderName ?? string.Empty;
        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT setval(pg_get_serial_sequence('\"GeneratedCalendarDrafts\"', 'Id'), 1, false);",
                ct);
        }
        else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name = 'GeneratedCalendarDrafts';", ct);
        }
    }

    private static void EnsureEditable(GeneratedCalendarDraft draft)
    {
        if (draft.Status != GeneratedCalendarDraftStatus.Draft)
            throw new InvalidOperationException("Sadece taslak durumundaki takvim düzenlenebilir.");
        if (draft.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Takvim taslağının saklama süresi dolmuş.");
    }

    private static int CalculateQualityScore(IReadOnlyList<CalendarWarningDto> warnings) =>
        Math.Max(0, 100 - warnings.Sum(w => w.Severity switch { "Error" => 20, "Warning" => 5, _ => 1 }));

    private static DateTime? ReadDate(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
        && DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;

    private static bool Matches(CalendarConstraintInputDto c, CalendarEventType eventType, string chainCode, string? templateItemKey = null, SemesterType? semester = null) =>
        (!c.TargetEventType.HasValue || c.TargetEventType.Value == eventType)
        && (string.IsNullOrWhiteSpace(c.TargetChainCode) || string.Equals(c.TargetChainCode, chainCode, StringComparison.OrdinalIgnoreCase))
        && MatchesTemplateItem(c, templateItemKey)
        && MatchesSemester(c, semester);

    private static bool MatchesTemplateItem(CalendarConstraintInputDto constraint, string? templateItemKey)
    {
        if (string.IsNullOrWhiteSpace(templateItemKey))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(constraint.ParameterJson);
            if (!doc.RootElement.TryGetProperty("templateItemKey", out var keyProperty) || keyProperty.ValueKind != JsonValueKind.String)
                return true;

            var constraintKey = keyProperty.GetString();
            return string.IsNullOrWhiteSpace(constraintKey) || string.Equals(constraintKey, templateItemKey, StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static bool MatchesSemester(CalendarConstraintInputDto constraint, SemesterType? semester)
    {
        if (!semester.HasValue)
            return true;

        try
        {
            using var doc = JsonDocument.Parse(constraint.ParameterJson);
            if (!doc.RootElement.TryGetProperty("semester", out var semesterProperty) || semesterProperty.ValueKind != JsonValueKind.String)
                return true;

            var constraintSemester = semesterProperty.GetString();
            return string.IsNullOrWhiteSpace(constraintSemester)
                || string.Equals(constraintSemester, semester.Value.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return true;
        }
    }

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

    private static int CountBusinessDaysAfter(DateTime fromExclusive, DateTime toInclusive, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var count = 0;
        for (var day = fromExclusive.Date.AddDays(1); day <= toInclusive.Date; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            if (holidays.Any(h => h.IsOfficialHoliday && h.BlocksAcademicEvents && day >= h.StartDate.Date && day <= h.EndDate.Date))
                continue;

            count++;
        }

        return count;
    }

    private static int InclusiveDays(DateTime start, DateTime end) =>
        Math.Max(0, (end.Date - start.Date).Days + 1);

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
