using System.Text.Json;
using SIS.Application.CalendarGenerator.DTOs;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;

namespace SIS.Infrastructure.Services.CalendarGenerator;

public partial class CalendarGeneratorService
{
    private static IReadOnlyDictionary<SemesterType, DateTime> BuildTermStarts(int startYear, GenerateCalendarRequestDto request) =>
        new Dictionary<SemesterType, DateTime>
        {
            [SemesterType.Fall] = EnsureUtcDate(request.FallStartDate ?? MondayOnOrAfter(new DateTime(startYear, 9, 22))),
            [SemesterType.Spring] = EnsureUtcDate(request.SpringStartDate ?? MondayOnOrAfter(new DateTime(startYear + 1, 2, 23))),
            [SemesterType.Summer] = EnsureUtcDate(MondayOnOrAfter(new DateTime(startYear + 1, 7, 13)))
        };

    private static GeneratedCalendarEventDto PlaceTemplateItem(
        string academicYear,
        string chainCode,
        CalendarTemplateItemDto item,
        SemesterType semester,
        DateTime termStart,
        IReadOnlyList<ResolvedHolidayDto> holidays,
        IReadOnlyList<CalendarConstraintInputDto> constraints,
        List<CalendarWarningDto> warnings)
    {
        var duration = ApplyDurationConstraint(Math.Max(1, item.DefaultDurationDays), item, chainCode, constraints);
        var start = termStart.AddDays(item.OffsetDays);
        var end = start.AddDays(duration - 1);

        var pin = FindPinConstraint(item, chainCode, constraints);
        if (pin != null)
        {
            start = pin.Value.Start;
            end = pin.Value.End ?? start.AddDays(duration - 1);
        }

        var prohibited = FindProhibitionOverlap(start, end, constraints);
        if (prohibited != null)
        {
            var delta = (prohibited.Value.End.Date - start.Date).Days + 1;
            start = start.AddDays(delta);
            end = end.AddDays(delta);
            warnings.Add(new("PROHIBITION_SHIFT", "Warning", $"{item.Title} yasak araliktan sonraya kaydirildi.", EventId(academicYear, semester, item.Key)));
        }

        if (item.AvoidHolidayConflict && IsCriticalEvent(item.EventType))
        {
            var attempts = 0;
            while ((OverlapsBlockingHoliday(start, end, holidays) || StartsOnWeekend(start, item.EventType)) && attempts < 14)
            {
                start = start.AddDays(1);
                end = end.AddDays(1);
                attempts++;
            }

            if (attempts > 0)
                warnings.Add(new("HOLIDAY_SHIFT", "Warning", $"{item.Title} tatil/onemli gun cakismasi nedeniyle {attempts} gun kaydirildi.", EventId(academicYear, semester, item.Key)));

            if (attempts >= 14)
                return BuildEvent(academicYear, semester, item, chainCode, start, end, true, "Otomatik yerlesim basarisiz.");
        }

        return BuildEvent(academicYear, semester, item, chainCode, start, end, false, null);
    }

    private static GeneratedCalendarEventDto BuildEvent(string academicYear, SemesterType semester, CalendarTemplateItemDto item, string chainCode, DateTime start, DateTime end, bool isFailed, string? note) =>
        new(
            EventId(academicYear, semester, item.Key),
            semester,
            semester.ToString(),
            item.EventType,
            CalendarEventTypeLabels.GetLabel(item.EventType),
            item.Title,
            EnsureUtcDate(start),
            start.Date == end.Date ? null : EnsureUtcDate(end),
            chainCode,
            item.Key,
            true,
            false,
            BlocksAcademicEvents(item.EventType),
            isFailed,
            note);

    private static IEnumerable<GeneratedCalendarEventDto> BuildHolidayEvents(string academicYear, IReadOnlyList<ResolvedHolidayDto> holidays, IReadOnlyDictionary<SemesterType, DateTime> termStarts, bool includeSummer)
    {
        var startYear = ParseAcademicYearStart(academicYear);
        var windowStart = Utc(startYear, 7, 1);
        var windowEnd = Utc(startYear + 1, 9, 10);

        foreach (var holiday in holidays.Where(h => h.StartDate <= windowEnd && h.EndDate >= windowStart))
        {
            var semester = InferSemester(holiday.StartDate, termStarts, includeSummer);
            if (semester == SemesterType.Summer && !includeSummer)
                continue;

            var eventType = holiday.Type switch
            {
                HolidayType.HijriCalculated => CalendarEventType.ReligiousHoliday,
                HolidayType.ImportantDay => CalendarEventType.ImportantDay,
                _ => CalendarEventType.NationalHoliday
            };

            yield return new GeneratedCalendarEventDto(
                $"holiday:{academicYear}:{semester}:{holiday.Name}:{holiday.StartDate:yyyyMMdd}",
                semester,
                semester.ToString(),
                eventType,
                CalendarEventTypeLabels.GetLabel(eventType),
                holiday.Name,
                EnsureUtcDate(holiday.StartDate),
                holiday.StartDate.Date == holiday.EndDate.Date ? null : EnsureUtcDate(holiday.EndDate),
                "HOLIDAY",
                null,
                true,
                true,
                holiday.BlocksAcademicEvents,
                false,
                holiday.IsEstimated ? "Dini gun tarihi Hicri fallback ile tahmini hesaplandi." : holiday.Source);
        }
    }

    private static SemesterType InferSemester(DateTime date, IReadOnlyDictionary<SemesterType, DateTime> termStarts, bool includeSummer)
    {
        if (date < termStarts[SemesterType.Spring].AddDays(-35))
            return SemesterType.Fall;
        if (!includeSummer || date < termStarts[SemesterType.Summer].AddDays(-20))
            return SemesterType.Spring;
        return SemesterType.Summer;
    }

    private static CalendarTemplateItemDto ApplyDurationOverride(CalendarTemplateItemDto item, Dictionary<string, int>? overrides) =>
        overrides != null && overrides.TryGetValue(item.Key, out var days) && days > 0
            ? item with { DefaultDurationDays = days }
            : item;

    private static int ApplyDurationConstraint(int currentDuration, CalendarTemplateItemDto item, string chainCode, IReadOnlyList<CalendarConstraintInputDto> constraints)
    {
        var match = constraints.Where(c => c.ConstraintType == CalendarConstraintType.Duration && Matches(c, item.EventType, chainCode))
            .OrderByDescending(c => c.IsHard).ThenBy(c => c.Priority).FirstOrDefault();
        if (match == null) return currentDuration;

        try
        {
            using var doc = JsonDocument.Parse(match.ParameterJson);
            if (doc.RootElement.TryGetProperty("days", out var days) && days.TryGetInt32(out var value) && value > 0)
                return value;
        }
        catch (JsonException) { }

        return currentDuration;
    }

    private static (DateTime Start, DateTime? End)? FindPinConstraint(CalendarTemplateItemDto item, string chainCode, IReadOnlyList<CalendarConstraintInputDto> constraints)
    {
        var match = constraints.Where(c => c.ConstraintType == CalendarConstraintType.Pin && Matches(c, item.EventType, chainCode))
            .OrderByDescending(c => c.IsHard).ThenBy(c => c.Priority).FirstOrDefault();
        if (match == null) return null;

        try
        {
            using var doc = JsonDocument.Parse(match.ParameterJson);
            var start = ReadDate(doc.RootElement, "startDate") ?? ReadDate(doc.RootElement, "date");
            var end = ReadDate(doc.RootElement, "endDate");
            return start.HasValue ? (EnsureUtcDate(start.Value), end.HasValue ? EnsureUtcDate(end.Value) : null) : null;
        }
        catch (JsonException) { return null; }
    }

    private static (DateTime Start, DateTime End)? FindProhibitionOverlap(DateTime start, DateTime end, IReadOnlyList<CalendarConstraintInputDto> constraints)
    {
        foreach (var constraint in constraints.Where(c => c.ConstraintType == CalendarConstraintType.Prohibition).OrderBy(c => c.Priority))
        {
            try
            {
                using var doc = JsonDocument.Parse(constraint.ParameterJson);
                var from = ReadDate(doc.RootElement, "from");
                var to = ReadDate(doc.RootElement, "to");
                if (from.HasValue && to.HasValue && DateRangesOverlap(start, end, from.Value, to.Value))
                    return (EnsureUtcDate(from.Value), EnsureUtcDate(to.Value));
            }
            catch (JsonException) { }
        }
        return null;
    }
}
