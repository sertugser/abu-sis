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
    private const int MakeupExamDurationDays = 7;
    private const int MidtermEarliestTeachingWeek = 7;
    private const int MidtermDefaultTeachingWeek = 8;
    private const int ExcuseExamMinBusinessDays = 3;
    private const int ExcuseExamMaxBusinessDays = 10;
    private const int FinalPreparationTeachingWeeks = 7;
    private const int FlexibleHolidayOverlapMinDays = 15;

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
            CheckOrder(list, group.Key, CalendarEventType.AddDrop, CalendarEventType.MidtermExam, warnings);
            CheckOrder(list, group.Key, CalendarEventType.MidtermExam, CalendarEventType.ExcuseExamApplication, warnings, secondKeyContains: "midterm_excuse_application");
            CheckOrder(list, group.Key, CalendarEventType.ExcuseExamApplication, CalendarEventType.ExcuseExam, warnings, firstKeyContains: "midterm_excuse_application", secondKeyContains: "midterm_excuse_exam");
            CheckOrder(list, group.Key, CalendarEventType.ExcuseExam, CalendarEventType.MidtermGradePublication, warnings, firstKeyContains: "midterm_excuse_exam", secondKeyContains: "midterm_publication");
            CheckOrder(list, group.Key, CalendarEventType.MidtermGradePublication, CalendarEventType.FinalExam, warnings, firstKeyContains: "midterm_publication", secondKeyContains: "final_exam");
            CheckOrder(list, group.Key, CalendarEventType.ClassesEnd, CalendarEventType.FinalExam, warnings);
            CheckOrder(list, group.Key, CalendarEventType.FinalExam, CalendarEventType.FinalGradePublication, warnings);
            CheckOrder(list, group.Key, CalendarEventType.FinalGradePublication, CalendarEventType.MakeupExam, warnings, secondKeyContains: "makeup_exam");
            CheckOrder(list, group.Key, CalendarEventType.MakeupExam, CalendarEventType.MakeupGradePublication, warnings, firstKeyContains: "makeup_exam");

            var withdrawal = First(list, CalendarEventType.CourseWithdrawal);
            var midterm = FirstByTemplateKey(list, "midterm_exam") ?? First(list, CalendarEventType.MidtermExam);
            if (withdrawal != null && midterm != null && withdrawal.StartDate.Date < midterm.StartDate.Date)
            {
                warnings.Add(new(
                    "CHRONOLOGY",
                    "Error",
                    $"{SemesterLabel(group.Key)}: {withdrawal.Title} etkinliÃ„Å¸i {midterm.Title} baÃ…Å¸lamadan ÃƒÂ¶nce olamaz.",
                    withdrawal.DraftEventId));
            }
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
            warnings.Add(new("CHRONOLOGY", "Error", $"{SemesterLabel(semester)}: {firstEvent.Title} etkinliÃ„Å¸i {secondEvent.Title} etkinliÃ„Å¸inden sonra bitiyor.", secondEvent.DraftEventId));
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
            yield return new CalendarWarningDto("REG_REQUIRED_EVENT", "Error", $"{SemesterLabel(semester)}: YÃƒÂ¶netmelik kontrolÃƒÂ¼ iÃƒÂ§in ders baÃ…Å¸langÃ„Â±cÃ„Â±, ders bitiÃ…Å¸i ve final sÃ„Â±navÃ„Â± bulunmalÃ„Â±dÃ„Â±r.");
            yield break;
        }

        var classesEndDate = classesEnd.EndDate ?? classesEnd.StartDate;
        var teachingPeriodDays = InclusiveDays(classesStart.StartDate, classesEndDate);
        if (teachingPeriodDays < RegularTeachingPeriodDays)
        {
            yield return new CalendarWarningDto(
                "REG_MIN_TEACHING_WEEKS",
                "Error",
                $"{SemesterLabel(semester)}: Madde 21 gereÃ„Å¸i kayÃ„Â±t ve sÃ„Â±navlar hariÃƒÂ§ ders sÃƒÂ¼resi en az 14 hafta olmalÃ„Â±dÃ„Â±r ({teachingPeriodDays} gÃƒÂ¼n).",
                classesEnd.DraftEventId);
        }

        var semesterWithExamsDays = InclusiveDays(classesStart.StartDate, finalExam.EndDate ?? finalExam.StartDate);
        if (semesterWithExamsDays < RegularSemesterWithExamsDays)
        {
            yield return new CalendarWarningDto(
                "REG_MIN_SEMESTER_WEEKS",
                "Error",
                $"{SemesterLabel(semester)}: Madde 7 gereÃ„Å¸i yarÃ„Â±yÃ„Â±l sÃ„Â±navlar dahil en az 16 hafta olmalÃ„Â±dÃ„Â±r ({semesterWithExamsDays} gÃƒÂ¼n).",
                finalExam.DraftEventId);
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
            yield return new CalendarWarningDto("REG_SUMMER_REQUIRED_EVENT", "Error", "Yaz: YÃƒÂ¶netmelik kontrolÃƒÂ¼ iÃƒÂ§in ders baÃ…Å¸langÃ„Â±cÃ„Â±, ders bitiÃ…Å¸i ve yaz okulu sÃ„Â±navÃ„Â± bulunmalÃ„Â±dÃ„Â±r.");
            yield break;
        }

        var classesEndDate = classesEnd.EndDate ?? classesEnd.StartDate;
        var teachingWeekdays = CountWeekdays(classesStart.StartDate, classesEndDate);
        if (teachingWeekdays < SummerTeachingWeekdays)
        {
            yield return new CalendarWarningDto(
                "REG_SUMMER_TEACHING_WEEKS",
                "Error",
                $"Yaz: Madde 22 gereÃ„Å¸i yaz okulu sÃ„Â±navlarÃ„Â± hariÃƒÂ§ 7 haftalÃ„Â±k ders sÃƒÂ¼resini kapsamalÃ„Â±dÃ„Â±r ({teachingWeekdays} hafta iÃƒÂ§i gÃƒÂ¼n).",
                classesEnd.DraftEventId);
        }

        if (summerExam.StartDate.Date <= classesEndDate.Date)
        {
            yield return new CalendarWarningDto(
                "REG_SUMMER_EXAM_ORDER",
                "Error",
                "Yaz: Madde 22 gereÃ„Å¸i yaz okulu sÃ„Â±navlarÃ„Â± ders sÃƒÂ¼resi tamamlandÃ„Â±ktan sonra baÃ…Å¸lamalÃ„Â±dÃ„Â±r.",
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
                $"{SemesterLabel(semester)}: Madde 36 gereÃ„Å¸i {publication.Title} sÃ„Â±nav dÃƒÂ¶nemini izleyen en geÃƒÂ§ 10 iÃ…Å¸ gÃƒÂ¼nÃƒÂ¼ iÃƒÂ§inde ilan edilmelidir ({businessDays} iÃ…Å¸ gÃƒÂ¼nÃƒÂ¼).",
                publication.DraftEventId);
        }
    }

    private static IEnumerable<CalendarWarningDto> ValidateMidtermAndFinalSpacing(IReadOnlyList<GeneratedCalendarEventDto> events, IReadOnlyList<ResolvedHolidayDto> holidays, SemesterType semester)
    {
        yield break;
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
        var gradeEntryStart = First(events, CalendarEventType.MidtermGradePublication)
            ?? First(events, CalendarEventType.FinalGradePublication)
            ?? First(events, CalendarEventType.SummerSchoolGradePublication);
        var gradeEntryEnd = First(events, CalendarEventType.MakeupGradePublication)
            ?? First(events, CalendarEventType.FinalGradePublication)
            ?? First(events, CalendarEventType.SummerSchoolGradePublication);

        term.TermStart = classesStart.StartDate;
        term.TermEnd = classesEnd.StartDate;

        var now = DateTime.UtcNow;
        term.AcademicCalendar ??= new TermAcademicCalendar
        {
            TermId = term.Id,
            CreatedAt = now
        };

        var cal = term.AcademicCalendar;
        cal.RegistrationStart = registration?.StartDate ?? classesStart.StartDate;
        cal.RegistrationEnd = registration?.EndDate ?? registration?.StartDate ?? classesStart.StartDate;
        cal.AddDropStart = addDrop?.StartDate ?? cal.RegistrationStart;
        cal.AddDropEnd = addDrop?.EndDate ?? addDrop?.StartDate ?? cal.RegistrationEnd;
        cal.WithdrawalDeadline = withdrawal?.StartDate ?? classesEnd.StartDate;
        cal.GradeEntryStart = gradeEntryStart?.StartDate ?? classesEnd.StartDate;
        cal.GradeEntryEnd = gradeEntryEnd?.EndDate ?? gradeEntryEnd?.StartDate ?? classesEnd.StartDate.AddDays(21);
        cal.UpdatedAt = now;
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
        var termType = semester switch
        {
            SemesterType.Fall => TermType.Fall,
            SemesterType.Spring => TermType.Spring,
            SemesterType.Summer => TermType.Summer,
            _ => TermType.Fall
        };
        return new AcademicTerm
        {
            Name = $"{academicYear} {label}",
            ShortName = $"{startYear % 100:00}-{(startYear + 1) % 100:00} {label}",
            EnglishName = $"{english} {academicYear}",
            ShortEnglishName = english,
            Year = startYear,
            TermType = termType,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string SemesterLabel(SemesterType semester) =>
        semester switch
        {
            SemesterType.Fall => "GÃƒÂ¼z",
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

    private static bool RemoveLegacyShiftWarnings(GeneratedCalendarDraft draft)
    {
        List<CalendarWarningDto> warnings;
        try
        {
            warnings = Deserialize<List<CalendarWarningDto>>(draft.WarningsJson);
        }
        catch (JsonException)
        {
            return false;
        }

        var cleanedWarnings = warnings.Where(w => !IsLegacyShiftWarning(w)).ToList();
        if (cleanedWarnings.Count == warnings.Count)
            return false;

        draft.WarningsJson = Serialize(cleanedWarnings);
        draft.QualityScore = CalculateQualityScore(cleanedWarnings);
        return true;
    }

    private static bool IsLegacyShiftWarning(CalendarWarningDto warning) =>
        warning.Code is "HOLIDAY_SHIFT" or "PROHIBITION_SHIFT"
        && !warning.Message.Contains("Yeni tarih:", StringComparison.OrdinalIgnoreCase);

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
            throw new InvalidOperationException("Sadece taslak durumundaki takvim dÃƒÂ¼zenlenebilir.");
        if (draft.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Takvim taslaÃ„Å¸Ã„Â±nÃ„Â±n saklama sÃƒÂ¼resi dolmuÃ…Å¸.");
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

    private static IReadOnlyList<string> FindBlockingConflictDescriptions(DateTime start, DateTime end, CalendarEventType eventType, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var descriptions = holidays
            .Where(h => h.BlocksAcademicEvents && DateRangesOverlap(start, end, h.StartDate, h.EndDate))
            .OrderBy(h => h.StartDate)
            .ThenBy(h => h.Name)
            .Select(h => $"{h.Name} ({FormatDateRange(h.StartDate, h.EndDate)})")
            .ToList();

        if (StartsOnWeekend(start, eventType))
            descriptions.Add($"hafta sonu ({FormatDate(start)})");

        return descriptions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FormatDate(DateTime date) =>
        date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private static string FormatDateRange(DateTime start, DateTime end)
    {
        var startText = FormatDate(start);
        var endText = FormatDate(end);
        return startText == endText ? startText : $"{startText} - {endText}";
    }

    private static bool DateRangesOverlap(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
        aStart.Date <= bEnd.Date && bStart.Date <= aEnd.Date;

    private static bool StartsOnWeekend(DateTime start, CalendarEventType eventType) =>
        IsSingleDaySensitive(eventType) && start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static bool ShouldShiftBackwardForWeekend(CalendarEventType eventType, DateTime start, IReadOnlyList<ResolvedHolidayDto> holidays) =>
        StartsOnWeekend(start, eventType)
        && !holidays.Any(h => h.BlocksAcademicEvents && DateRangesOverlap(start, start, h.StartDate, h.EndDate));

    private static bool MustStartOnMonday(CalendarEventType eventType) =>
        eventType is CalendarEventType.ClassesStart
            or CalendarEventType.CourseRegistration
            or CalendarEventType.AddDrop
            or CalendarEventType.MidtermExam
            or CalendarEventType.FinalExam
            or CalendarEventType.MakeupExam
            or CalendarEventType.ExcuseExam
            or CalendarEventType.PrepSchoolExam
            or CalendarEventType.SummerSchoolExam;

    private static DateTime MondayOnOrAfter(DateTime date)
    {
        var current = date.Date;
        while (current.DayOfWeek != DayOfWeek.Monday) current = current.AddDays(1);
        return DateTime.SpecifyKind(current, DateTimeKind.Utc);
    }

    private static DateTime MondayOnOrBefore(DateTime date)
    {
        var current = date.Date;
        while (current.DayOfWeek != DayOfWeek.Monday) current = current.AddDays(-1);
        return DateTime.SpecifyKind(current, DateTimeKind.Utc);
    }

    private static DateTime NearestMonday(DateTime date)
    {
        var previousMonday = MondayOnOrBefore(date);
        var nextMonday = MondayOnOrAfter(date);
        var previousDistance = Math.Abs((date.Date - previousMonday.Date).Days);
        var nextDistance = Math.Abs((nextMonday.Date - date.Date).Days);
        return previousDistance <= nextDistance ? previousMonday : nextMonday;
    }

    private static int ResolveConflictShiftDays(CalendarEventType eventType, bool shiftBackwardForWeekend) =>
        MustStartOnMonday(eventType) && !shiftBackwardForWeekend ? 7 : 1;

    private static bool IsCriticalEvent(CalendarEventType type) =>
        type is CalendarEventType.ClassesStart or CalendarEventType.ClassesEnd
            or CalendarEventType.CourseWithdrawal or CalendarEventType.RegistrationFreezeDeadline
            or CalendarEventType.MidtermExam or CalendarEventType.FinalExam or CalendarEventType.MakeupExamApplication or CalendarEventType.MakeupExam
            or CalendarEventType.ExcuseExam or CalendarEventType.MidtermGradePublication
            or CalendarEventType.FinalGradePublication or CalendarEventType.MakeupGradePublication
            or CalendarEventType.SummerSchoolRegistration or CalendarEventType.SummerSchoolExam
            or CalendarEventType.SummerSchoolGradePublication;

    private static bool ShouldTrimTrailingWeekend(GeneratedCalendarEventDto ev) =>
        !ev.IsHoliday
        && !IsCriticalEvent(ev.EventType)
        && ev.EndDate.HasValue
        && ev.EndDate.Value.Date > ev.StartDate.Date;

    private static bool CanOverlapBlockingHoliday(
        CalendarEventType type,
        DateTime start,
        DateTime end,
        IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        if (type is CalendarEventType.CourseRegistration or CalendarEventType.AddDrop)
            return true;

        if (IsCriticalEvent(type) || InclusiveDays(start, end) < FlexibleHolidayOverlapMinDays)
            return false;

        var overlaps = holidays
            .Where(h => h.BlocksAcademicEvents && DateRangesOverlap(start, end, h.StartDate, h.EndDate))
            .ToList();
        return overlaps.Count > 0 && overlaps.All(h => h.Type is not HolidayType.HijriCalculated);
    }

    private static string? BlockingHolidayOverlapError(
        string title,
        CalendarEventType type,
        DateTime start,
        DateTime end,
        IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var overlaps = holidays
            .Where(h => h.BlocksAcademicEvents && DateRangesOverlap(start, end, h.StartDate, h.EndDate))
            .OrderBy(h => h.StartDate)
            .ThenBy(h => h.Name)
            .ToList();
        if (overlaps.Count == 0)
            return null;

        if (CanOverlapBlockingHoliday(type, start, end, holidays))
            return null;

        var overlapLabel = overlaps.Any(h => h.Type == HolidayType.HijriCalculated)
            ? "dini bayram"
            : "resmi tatil veya bloklayÄ±cÄ± Ã¶nemli gÃ¼n";
        return $"{title} {overlapLabel} Ã¼zerine taÅŸÄ±namaz.";
    }

    private static bool RequiresSequenceAwareHolidayPlacement(CalendarEventType type) =>
        type is CalendarEventType.MidtermExam;

    private static bool IsSingleDaySensitive(CalendarEventType type) =>
        type is CalendarEventType.ClassesStart or CalendarEventType.ClassesEnd or CalendarEventType.CourseWithdrawal
            or CalendarEventType.RegistrationFreezeDeadline or CalendarEventType.MidtermGradePublication
            or CalendarEventType.FinalGradePublication or CalendarEventType.MakeupGradePublication
            or CalendarEventType.ExcuseExamApplication or CalendarEventType.MakeupExamApplication or CalendarEventType.SummerSchoolGradePublication;

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

    private static int CountTeachingWeeksStartedByDate(DateTime classesStart, DateTime date, IReadOnlyList<DateTime> heavyWeeks)
    {
        var count = 0;
        for (var weekStart = MondayOnOrAfter(classesStart); weekStart <= date.Date; weekStart = weekStart.AddDays(7))
        {
            if (heavyWeeks.Any(week => week.Date == weekStart.Date))
                continue;

            count++;
        }

        return count;
    }

    private static int CountTeachingWeeksBetween(DateTime fromExclusive, DateTime toInclusive, IReadOnlyList<DateTime> heavyWeeks)
    {
        var count = 0;
        for (var weekStart = MondayOnOrAfter(fromExclusive.AddDays(1)); weekStart < toInclusive.Date; weekStart = weekStart.AddDays(7))
        {
            if (heavyWeeks.Any(week => week.Date == weekStart.Date))
                continue;

            count++;
        }

        return count;
    }

    private static List<GeneratedCalendarEventDto> ApplyDependentExamScheduling(
        IReadOnlyList<GeneratedCalendarEventDto> events,
        IReadOnlyList<ResolvedHolidayDto> holidays,
        IReadOnlySet<string>? pinnedDraftEventIds = null)
    {
        var updated = events.ToList();

        foreach (var semester in new[] { SemesterType.Fall, SemesterType.Spring })
        {
            var finalExam = updated.SingleOrDefault(e =>
                e.Semester == semester && string.Equals(e.TemplateItemKey, "final_exam", StringComparison.OrdinalIgnoreCase));
            var finalPublication = updated.SingleOrDefault(e =>
                e.Semester == semester && string.Equals(e.TemplateItemKey, "final_publication", StringComparison.OrdinalIgnoreCase));
            var makeupApplication = updated.SingleOrDefault(e =>
                e.Semester == semester && string.Equals(e.TemplateItemKey, "makeup_application", StringComparison.OrdinalIgnoreCase));
            var makeupExam = updated.SingleOrDefault(e =>
                e.Semester == semester && string.Equals(e.TemplateItemKey, "makeup_exam", StringComparison.OrdinalIgnoreCase));

            if (finalExam == null || finalPublication == null || makeupExam == null)
                continue;

            var finalExamEnd = finalExam.EndDate ?? finalExam.StartDate;
            var fifthBusinessDay = NthBusinessDayAfter(finalExamEnd, 5, holidays);
            var resultsBufferDate = finalPublication.StartDate.AddDays(4);
            var makeupStart = MaxDate(fifthBusinessDay, resultsBufferDate);
            makeupStart = AlignMakeupStart(makeupStart, holidays);

            if (IsPinned(makeupExam, pinnedDraftEventIds))
            {
                makeupStart = makeupExam.StartDate;
            }
            else
            {
                updated = ReplaceEvent(updated, makeupExam with
                {
                    StartDate = makeupStart,
                    EndDate = makeupStart.AddDays(MakeupExamDurationDays - 1)
                });
            }

            if (makeupApplication != null && !IsPinned(makeupApplication, pinnedDraftEventIds))
            {
                var applicationDeadline = PreviousBusinessDayBefore(makeupStart, holidays);
                updated = ReplaceEvent(updated, makeupApplication with
                {
                    StartDate = applicationDeadline,
                    EndDate = null
                });
            }
        }

        return updated;
    }

    private static List<GeneratedCalendarEventDto> NormalizeCalendarEvents(
        IReadOnlyList<GeneratedCalendarEventDto> events,
        IReadOnlyList<ResolvedHolidayDto> holidays,
        IReadOnlySet<string>? pinnedDraftEventIds = null)
    {
        var normalized = events.ToList();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var before = DateFingerprint(normalized);

            normalized = ApplyHolidayOverflowShifts(normalized, holidays, pinnedDraftEventIds);
            normalized = ApplyRegularSemesterSequenceRules(normalized, holidays, pinnedDraftEventIds);
            normalized = ApplySummerExcuseSequenceRules(normalized, holidays, pinnedDraftEventIds);
            normalized = ApplyGradePublicationSequenceRules(normalized, holidays, pinnedDraftEventIds);
            normalized = ApplyDependentExamScheduling(normalized, holidays, pinnedDraftEventIds);
            normalized = ApplyHolidayOverflowShifts(normalized, holidays, pinnedDraftEventIds);
            normalized = ApplySummerExcuseSequenceRules(normalized, holidays, pinnedDraftEventIds);
            normalized = ApplyGradePublicationSequenceRules(normalized, holidays, pinnedDraftEventIds);
            normalized = TrimTrailingWeekendEnds(normalized, pinnedDraftEventIds);

            if (DateFingerprint(normalized) == before)
                break;
        }

        return normalized;
    }

    private static List<GeneratedCalendarEventDto> ApplyGradePublicationSequenceRules(
        IReadOnlyList<GeneratedCalendarEventDto> events,
        IReadOnlyList<ResolvedHolidayDto> holidays,
        IReadOnlySet<string>? pinnedDraftEventIds = null)
    {
        var updated = events.ToList();

        foreach (var semester in new[] { SemesterType.Fall, SemesterType.Spring })
        {
            updated = EnsurePublicationAfterEvent(updated, semester, "midterm_excuse_exam", "midterm_publication", holidays, pinnedDraftEventIds);
            updated = EnsurePublicationAfterEvent(updated, semester, "final_exam", "final_publication", holidays, pinnedDraftEventIds);
            updated = EnsurePublicationAfterEvent(updated, semester, "makeup_exam", "makeup_publication", holidays, pinnedDraftEventIds);
        }

        updated = EnsurePublicationAfterEvent(updated, SemesterType.Summer, "summer_excuse_exam", "summer_publication", holidays, pinnedDraftEventIds);
        updated = EnsurePublicationAfterEvent(updated, SemesterType.Summer, "summer_exam", "summer_publication", holidays, pinnedDraftEventIds);

        return updated;
    }

    private static List<GeneratedCalendarEventDto> EnsurePublicationAfterEvent(
        List<GeneratedCalendarEventDto> events,
        SemesterType semester,
        string sourceKey,
        string publicationKey,
        IReadOnlyList<ResolvedHolidayDto> holidays,
        IReadOnlySet<string>? pinnedDraftEventIds = null)
    {
        var source = events.SingleOrDefault(e =>
            e.Semester == semester && string.Equals(e.TemplateItemKey, sourceKey, StringComparison.OrdinalIgnoreCase));
        var publication = events.SingleOrDefault(e =>
            e.Semester == semester && string.Equals(e.TemplateItemKey, publicationKey, StringComparison.OrdinalIgnoreCase));
        if (source == null || publication == null)
            return events;
        if (IsPinned(publication, pinnedDraftEventIds))
            return events;

        var sourceEnd = source.EndDate ?? source.StartDate;
        DateTime? adjustedStart = null;
        if (publication.StartDate.Date >= source.StartDate.Date && publication.StartDate.Date < sourceEnd.Date)
            adjustedStart = NthBusinessDayAfter(sourceEnd, 3, holidays);
        return adjustedStart.HasValue
            ? ReplaceEvent(events, publication with { StartDate = adjustedStart.Value, EndDate = null })
            : events;
    }

    private static List<GeneratedCalendarEventDto> ApplyHolidayOverflowShifts(
        IReadOnlyList<GeneratedCalendarEventDto> events,
        IReadOnlyList<ResolvedHolidayDto> holidays,
        IReadOnlySet<string>? pinnedDraftEventIds = null)
    {
        var updated = events.ToList();

        foreach (var ev in events.Where(e =>
            !e.IsHoliday
            && !IsPinned(e, pinnedDraftEventIds)
            && !CanOverlapBlockingHoliday(e.EventType, e.StartDate, e.EndDate ?? e.StartDate, holidays)
            && !RequiresSequenceAwareHolidayPlacement(e.EventType)))
        {
            var shifted = ShiftEventOffBlockingHolidays(ev, holidays);
            if (shifted.StartDate != ev.StartDate || shifted.EndDate != ev.EndDate)
                updated = ReplaceEvent(updated, shifted);
        }

        return updated;
    }

    private static List<GeneratedCalendarEventDto> AlignClassesEndToWeekday(
        IReadOnlyList<GeneratedCalendarEventDto> events,
        IReadOnlySet<string>? pinnedDraftEventIds = null)
    {
        var updated = events.ToList();

        foreach (var classesEnd in events.Where(e => !e.IsHoliday && e.EventType == CalendarEventType.ClassesEnd && !IsPinned(e, pinnedDraftEventIds)))
        {
            var alignedDate = WeekdayOnOrBefore(classesEnd.StartDate);
            if (alignedDate.Date != classesEnd.StartDate.Date)
                updated = ReplaceEvent(updated, classesEnd with { StartDate = alignedDate, EndDate = null });
        }

        return updated;
    }

    private static List<GeneratedCalendarEventDto> TrimTrailingWeekendEnds(
        IReadOnlyList<GeneratedCalendarEventDto> events,
        IReadOnlySet<string>? pinnedDraftEventIds = null)
    {
        var updated = events.ToList();

        foreach (var ev in events.Where(ShouldTrimTrailingWeekend))
        {
            var trimmedEnd = WeekdayOnOrBefore(ev.EndDate!.Value);
            if (trimmedEnd.Date == ev.EndDate.Value.Date || trimmedEnd.Date < ev.StartDate.Date)
                continue;

            updated = ReplaceEvent(updated, ev with
            {
                EndDate = trimmedEnd.Date == ev.StartDate.Date ? null : trimmedEnd
            });
        }

        return updated;
    }

    private static DateTime WeekdayOnOrBefore(DateTime date)
    {
        var day = date.Date;
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            day = day.AddDays(-1);
        return EnsureUtcDate(day);
    }

    private static GeneratedCalendarEventDto ShiftEventOffBlockingHolidays(
        GeneratedCalendarEventDto ev,
        IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var durationDays = ((ev.EndDate ?? ev.StartDate).Date - ev.StartDate.Date).Days;
        var start = ev.StartDate;
        var end = ev.EndDate ?? ev.StartDate;
        var attempts = 0;

        while (attempts < 90 && OverlapsBlockingHoliday(start, end, holidays))
        {
            var blockingHolidayEnd = holidays
                .Where(h => h.BlocksAcademicEvents && DateRangesOverlap(start, end, h.StartDate, h.EndDate))
                .Max(h => h.EndDate.Date);

            start = EnsureUtcDate(blockingHolidayEnd.AddDays(1));
            if (MustStartOnMonday(ev.EventType))
                start = MondayOnOrAfter(start);

            end = EnsureUtcDate(start.AddDays(durationDays));
            attempts++;
        }

        if (start == ev.StartDate && end == (ev.EndDate ?? ev.StartDate))
            return ev;

        return ev with
        {
            StartDate = start,
            EndDate = durationDays == 0 ? null : end
        };
    }

    private static List<GeneratedCalendarEventDto> ApplyRegularSemesterSequenceRules(
        IReadOnlyList<GeneratedCalendarEventDto> events,
        IReadOnlyList<ResolvedHolidayDto> holidays,
        IReadOnlySet<string>? pinnedDraftEventIds = null)
    {
        var updated = events.ToList();

        foreach (var semester in new[] { SemesterType.Fall, SemesterType.Spring })
        {
            var classesStart = updated.SingleOrDefault(e =>
                e.Semester == semester && string.Equals(e.TemplateItemKey, "classes_start", StringComparison.OrdinalIgnoreCase));
            var midterm = updated.SingleOrDefault(e =>
                e.Semester == semester && string.Equals(e.TemplateItemKey, "midterm_exam", StringComparison.OrdinalIgnoreCase));
            var finalExam = updated.SingleOrDefault(e =>
                e.Semester == semester && string.Equals(e.TemplateItemKey, "final_exam", StringComparison.OrdinalIgnoreCase));
            if (classesStart == null || midterm == null || finalExam == null)
                continue;

            var heavyWeeks = GetHeavyReligiousHolidayWeeks(classesStart.StartDate, finalExam.StartDate, holidays);
            var selectedMidterm = SelectMidtermPlacement(midterm, classesStart, finalExam, heavyWeeks, holidays);
            if (!IsPinned(midterm, pinnedDraftEventIds)
                && (selectedMidterm.StartDate != midterm.StartDate || selectedMidterm.EndDate != midterm.EndDate))
            {
                midterm = selectedMidterm;
                updated = ReplaceEvent(updated, midterm);
            }

            var countedWeeksBeforeFinal = CountTeachingWeeksBetween(midterm.EndDate ?? midterm.StartDate, finalExam.StartDate, heavyWeeks);
            if (countedWeeksBeforeFinal < FinalPreparationTeachingWeeks)
            {
                var candidate = midterm with
                {
                    StartDate = midterm.StartDate.AddDays(-7),
                    EndDate = midterm.EndDate?.AddDays(-7)
                };

                if (!IsPinned(midterm, pinnedDraftEventIds)
                    && !OverlapsBlockingHoliday(candidate.StartDate, candidate.EndDate ?? candidate.StartDate, holidays))
                {
                    midterm = candidate;
                    updated = ReplaceEvent(updated, midterm);
                }
            }

            var midtermEnd = midterm.EndDate ?? midterm.StartDate;

            var withdrawal = updated.SingleOrDefault(e =>
                e.Semester == semester && string.Equals(e.TemplateItemKey, "withdrawal_deadline", StringComparison.OrdinalIgnoreCase));
            if (withdrawal != null && !IsPinned(withdrawal, pinnedDraftEventIds) && withdrawal.StartDate.Date < midterm.StartDate.Date)
                updated = ReplaceEvent(updated, withdrawal with { StartDate = midterm.StartDate, EndDate = null });

            var freeze = updated.SingleOrDefault(e =>
                e.Semester == semester && string.Equals(e.TemplateItemKey, "freeze_deadline", StringComparison.OrdinalIgnoreCase));
            if (freeze != null && !IsPinned(freeze, pinnedDraftEventIds) && freeze.StartDate.Date < midterm.StartDate.Date)
                updated = ReplaceEvent(updated, freeze with { StartDate = midterm.StartDate, EndDate = null });

            var excuseApplication = updated.SingleOrDefault(e =>
                e.Semester == semester && string.Equals(e.TemplateItemKey, "midterm_excuse_application", StringComparison.OrdinalIgnoreCase));
            if (excuseApplication != null && !IsPinned(excuseApplication, pinnedDraftEventIds))
            {
                var desiredApplicationStart = NextBusinessDayAfter(midtermEnd, holidays);
                if (excuseApplication.StartDate.Date != desiredApplicationStart.Date || excuseApplication.EndDate != null)
                    updated = ReplaceEvent(updated, excuseApplication with { StartDate = desiredApplicationStart, EndDate = null });
            }

            var excuseExam = updated.SingleOrDefault(e =>
                e.Semester == semester && string.Equals(e.TemplateItemKey, "midterm_excuse_exam", StringComparison.OrdinalIgnoreCase));
            if (excuseExam != null && !IsPinned(excuseExam, pinnedDraftEventIds))
            {
                var desiredStart = NextExamWeekMondayAfter(midtermEnd);
                var durationDays = EventBusinessDayDuration(excuseExam, holidays);
                var desiredEnd = NthBusinessDayAfter(desiredStart, durationDays - 1, holidays);
                if (excuseExam.StartDate.Date != desiredStart.Date || (excuseExam.EndDate ?? excuseExam.StartDate).Date != desiredEnd.Date)
                    updated = ReplaceEvent(updated, excuseExam with { StartDate = desiredStart, EndDate = durationDays == 1 ? null : desiredEnd });
            }

            var midtermPublication = updated.SingleOrDefault(e =>
                e.Semester == semester && string.Equals(e.TemplateItemKey, "midterm_publication", StringComparison.OrdinalIgnoreCase));
            var refreshedExcuseExam = updated.SingleOrDefault(e =>
                e.Semester == semester && string.Equals(e.TemplateItemKey, "midterm_excuse_exam", StringComparison.OrdinalIgnoreCase));
            if (midtermPublication != null && refreshedExcuseExam != null && !IsPinned(midtermPublication, pinnedDraftEventIds))
            {
                var excuseEnd = refreshedExcuseExam.EndDate ?? refreshedExcuseExam.StartDate;
                var earliestPublication = NextBusinessDayAfter(excuseEnd, holidays);
                var preferredPublication = NthBusinessDayAfter(excuseEnd, 3, holidays);
                var latestAllowedPublication = NthBusinessDayAfter(midtermEnd, GradePublicationBusinessDayLimit, holidays);

                var desiredPublication = preferredPublication;
                if (desiredPublication.Date > latestAllowedPublication.Date)
                    desiredPublication = latestAllowedPublication.Date >= earliestPublication.Date
                        ? latestAllowedPublication
                        : earliestPublication;

                if (midtermPublication.StartDate.Date != desiredPublication.Date || midtermPublication.EndDate != null)
                    updated = ReplaceEvent(updated, midtermPublication with { StartDate = desiredPublication, EndDate = null });
            }
        }

        return updated;
    }

    private static List<GeneratedCalendarEventDto> ApplySummerExcuseSequenceRules(
        IReadOnlyList<GeneratedCalendarEventDto> events,
        IReadOnlyList<ResolvedHolidayDto> holidays,
        IReadOnlySet<string>? pinnedDraftEventIds = null)
    {
        var updated = events.ToList();

        var summerExam = updated.SingleOrDefault(e =>
            e.Semester == SemesterType.Summer && string.Equals(e.TemplateItemKey, "summer_exam", StringComparison.OrdinalIgnoreCase));
        if (summerExam == null)
            return updated;

        var summerExamEnd = summerExam.EndDate ?? summerExam.StartDate;
        var application = updated.SingleOrDefault(e =>
            e.Semester == SemesterType.Summer && string.Equals(e.TemplateItemKey, "summer_excuse_application", StringComparison.OrdinalIgnoreCase));
        if (application != null && !IsPinned(application, pinnedDraftEventIds))
        {
            var desiredApplicationStart = NextBusinessDayAfter(summerExamEnd, holidays);
            if (application.StartDate.Date != desiredApplicationStart.Date || application.EndDate != null)
                updated = ReplaceEvent(updated, application with { StartDate = desiredApplicationStart, EndDate = null });
        }

        var excuseExam = updated.SingleOrDefault(e =>
            e.Semester == SemesterType.Summer && string.Equals(e.TemplateItemKey, "summer_excuse_exam", StringComparison.OrdinalIgnoreCase));
        if (excuseExam != null && !IsPinned(excuseExam, pinnedDraftEventIds))
        {
            var desiredStart = NextExamWeekMondayAfter(summerExamEnd);
            var durationDays = EventBusinessDayDuration(excuseExam, holidays);
            var desiredEnd = NthBusinessDayAfter(desiredStart, durationDays - 1, holidays);
            if (excuseExam.StartDate.Date != desiredStart.Date || (excuseExam.EndDate ?? excuseExam.StartDate).Date != desiredEnd.Date)
                updated = ReplaceEvent(updated, excuseExam with { StartDate = desiredStart, EndDate = durationDays == 1 ? null : desiredEnd });
        }

        return updated;
    }

    private static GeneratedCalendarEventDto SelectMidtermPlacement(
        GeneratedCalendarEventDto midterm,
        GeneratedCalendarEventDto classesStart,
        GeneratedCalendarEventDto finalExam,
        IReadOnlyList<DateTime> heavyWeeks,
        IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var currentEnd = midterm.EndDate ?? midterm.StartDate;
        if (!OverlapsBlockingHoliday(midterm.StartDate, currentEnd, holidays))
            return midterm;

        var durationDays = (currentEnd.Date - midterm.StartDate.Date).Days;
        var blockingHolidayEnd = holidays
            .Where(h => h.BlocksAcademicEvents && DateRangesOverlap(midterm.StartDate, currentEnd, h.StartDate, h.EndDate))
            .Max(h => h.EndDate.Date);

        var candidates = new[]
            {
                midterm.StartDate.AddDays(-7),
                MondayOnOrAfter(blockingHolidayEnd.AddDays(1))
            }
            .Select(EnsureUtcDate)
            .Distinct()
            .Select(start => new
            {
                Start = start,
                End = EnsureUtcDate(start.AddDays(durationDays)),
                Before = CountTeachingWeeksBetween(classesStart.StartDate.AddDays(-1), start, heavyWeeks),
                After = CountTeachingWeeksBetween(start.AddDays(durationDays), finalExam.StartDate, heavyWeeks)
            })
            .Where(c =>
                c.Start.Date > classesStart.StartDate.Date
                && c.End.Date < finalExam.StartDate.Date
                && !OverlapsBlockingHoliday(c.Start, c.End, holidays))
            .ToList();

        var selected = candidates
            .Where(c => c.Before >= MidtermEarliestTeachingWeek)
            .OrderBy(c => Math.Abs(c.Before - c.After))
            .ThenByDescending(c => c.Before)
            .ThenByDescending(c => c.Start)
            .FirstOrDefault()
            ?? candidates
                .OrderByDescending(c => c.Before)
                .ThenBy(c => Math.Abs(c.Before - c.After))
                .ThenByDescending(c => c.Start)
                .FirstOrDefault();

        if (selected == null)
            return ShiftEventOffBlockingHolidays(midterm, holidays);

        return midterm with
        {
            StartDate = selected.Start,
            EndDate = durationDays == 0 ? null : selected.End
        };
    }

    private static List<GeneratedCalendarEventDto> ReplaceEvent(List<GeneratedCalendarEventDto> events, GeneratedCalendarEventDto replacement) =>
        events.Select(e => e.DraftEventId == replacement.DraftEventId ? replacement : e).ToList();

    private static bool IsPinned(GeneratedCalendarEventDto ev, IReadOnlySet<string>? pinnedDraftEventIds) =>
        pinnedDraftEventIds?.Contains(ev.DraftEventId) == true;

    private static string DateFingerprint(IReadOnlyList<GeneratedCalendarEventDto> events) =>
        string.Join("|", events
            .OrderBy(e => e.DraftEventId, StringComparer.Ordinal)
            .Select(e => $"{e.DraftEventId}:{e.StartDate:yyyyMMdd}:{(e.EndDate.HasValue ? e.EndDate.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture) : string.Empty)}"));

    private static List<DateTime> GetHeavyReligiousHolidayWeeks(DateTime semesterStart, DateTime semesterEnd, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var weeks = new List<DateTime>();
        for (var weekStart = MondayOnOrBefore(semesterStart); weekStart <= semesterEnd.Date; weekStart = weekStart.AddDays(7))
        {
            var weekEnd = weekStart.AddDays(6);
            var blockedDays = new HashSet<DateTime>();
            foreach (var holiday in holidays.Where(h => h.BlocksAcademicEvents && h.Type == HolidayType.HijriCalculated && DateRangesOverlap(weekStart, weekEnd, h.StartDate, h.EndDate)))
            {
                for (var day = holiday.StartDate.Date; day <= holiday.EndDate.Date; day = day.AddDays(1))
                {
                    if (day >= weekStart.Date && day <= weekEnd.Date)
                        blockedDays.Add(day);
                }
            }

            if (blockedDays.Count > 3)
                weeks.Add(EnsureUtcDate(weekStart));
        }

        return weeks;
    }

    private static DateTime GetTeachingWeekStart(DateTime classesStart, int teachingWeekNumber, IReadOnlyList<DateTime> heavyWeeks)
    {
        var current = MondayOnOrAfter(classesStart);
        var countedWeeks = 1;
        while (countedWeeks < teachingWeekNumber)
        {
            current = current.AddDays(7);
            if (heavyWeeks.Any(week => week.Date == current.Date))
                continue;

            countedWeeks++;
        }

        return EnsureUtcDate(current);
    }

    private static DateTime GetTeachingWeekStartAfter(DateTime date, int teachingWeeksAfter, IReadOnlyList<DateTime> heavyWeeks)
    {
        var current = MondayOnOrAfter(date.AddDays(1));
        var countedWeeks = 0;
        while (countedWeeks < teachingWeeksAfter)
        {
            if (!heavyWeeks.Any(week => week.Date == current.Date))
                countedWeeks++;

            if (countedWeeks == teachingWeeksAfter)
                break;

            current = current.AddDays(7);
        }

        return EnsureUtcDate(current);
    }

    private static DateTime NextBusinessDayAfter(DateTime fromExclusive, IReadOnlyList<ResolvedHolidayDto> holidays) =>
        NthBusinessDayAfter(fromExclusive, 1, holidays);

    private static DateTime NextExamWeekMondayAfter(DateTime fromExclusive) =>
        MondayOnOrAfter(fromExclusive.Date.AddDays(1));

    private static int EventBusinessDayDuration(GeneratedCalendarEventDto ev, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var end = ev.EndDate ?? ev.StartDate;
        var businessDays = 0;
        for (var day = ev.StartDate.Date; day <= end.Date; day = day.AddDays(1))
        {
            if (IsBusinessDay(day, holidays))
                businessDays++;
        }

        return Math.Max(1, businessDays);
    }

    private static DateTime ExtendExamForOfficialHolidays(DateTime start, DateTime initialEnd, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var end = initialEnd.Date;
        var day = start.Date;
        while (day <= end)
        {
            if (IsOfficialHoliday(day, holidays))
                end = end.AddDays(1);

            day = day.AddDays(1);
        }

        return EnsureUtcDate(end);
    }

    private static DateTime NthBusinessDayAfter(DateTime fromExclusive, int businessDayCount, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var count = 0;
        var day = fromExclusive.Date;
        while (count < businessDayCount)
        {
            day = day.AddDays(1);
            if (!IsBusinessDay(day, holidays))
                continue;

            count++;
        }

        return EnsureUtcDate(day);
    }

    private static DateTime PreviousBusinessDayBefore(DateTime date, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var day = date.Date.AddDays(-1);
        while (!IsBusinessDay(day, holidays))
            day = day.AddDays(-1);

        return EnsureUtcDate(day);
    }

    private static DateTime AlignMakeupStart(DateTime candidate, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var current = candidate.Date;
        while (IsOfficialHoliday(current, holidays))
            current = current.AddDays(1);

        if (current.DayOfWeek == DayOfWeek.Friday)
            current = current.AddDays(3);
        else if (current.DayOfWeek == DayOfWeek.Saturday)
            current = current.AddDays(2);
        else if (current.DayOfWeek == DayOfWeek.Sunday)
            current = current.AddDays(1);

        while (IsOfficialHoliday(current, holidays))
            current = current.AddDays(1);

        if (current.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            current = MondayOnOrAfter(current);

        return EnsureUtcDate(current);
    }

    private static bool IsBusinessDay(DateTime day, IReadOnlyList<ResolvedHolidayDto> holidays) =>
        day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday
        && !IsOfficialHoliday(day, holidays);

    private static bool IsOfficialHoliday(DateTime day, IReadOnlyList<ResolvedHolidayDto> holidays) =>
        holidays.Any(h => h.IsOfficialHoliday && h.BlocksAcademicEvents && day >= h.StartDate.Date && day <= h.EndDate.Date);

    private static DateTime MaxDate(DateTime left, DateTime right) =>
        left >= right ? left : right;

    private static int InclusiveDays(DateTime start, DateTime end) =>
        Math.Max(0, (end.Date - start.Date).Days + 1);

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


