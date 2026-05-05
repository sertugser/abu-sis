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
            [SemesterType.Fall] = EnsureUtcDate(NearestMonday(request.FallStartDate ?? new DateTime(startYear, 9, 22))),
            [SemesterType.Spring] = EnsureUtcDate(NearestMonday(request.SpringStartDate ?? new DateTime(startYear + 1, 2, 23))),
            [SemesterType.Summer] = EnsureUtcDate(NearestMonday(new DateTime(startYear + 1, 7, 13)))
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
        var duration = ApplyDurationConstraint(Math.Max(1, item.DefaultDurationDays), item, chainCode, semester, constraints);
        var start = termStart.AddDays(item.OffsetDays);
        var end = IsExamEvent(item.EventType) ? NthBusinessDayAfter(start, duration - 1, holidays) : start.AddDays(duration - 1);

        var pin = FindPinConstraint(item, chainCode, semester, constraints);
        var isPinnedEnd = false;
        if (pin != null)
        {
            start = pin.Value.Start;
            if (pin.Value.End.HasValue)
            {
                end = pin.Value.End.Value;
                isPinnedEnd = true;
            }
            else
            {
                end = IsExamEvent(item.EventType) ? NthBusinessDayAfter(start, duration - 1, holidays) : start.AddDays(duration - 1);
            }
        }

        if (MustStartOnMonday(item.EventType) && start.DayOfWeek != DayOfWeek.Monday)
        {
            var alignedStart = MondayOnOrAfter(start);
            var alignmentDelta = (alignedStart.Date - start.Date).Days;
            start = alignedStart;
            end = isPinnedEnd ? end.AddDays(alignmentDelta) : (IsExamEvent(item.EventType) ? NthBusinessDayAfter(start, duration - 1, holidays) : start.AddDays(duration - 1));
        }

        var prohibited = FindProhibitionOverlap(start, end, constraints);
        if (prohibited != null)
        {
            var delta = (prohibited.Value.End.Date - start.Date).Days + 1;
            start = start.AddDays(delta);
            if (MustStartOnMonday(item.EventType) && start.DayOfWeek != DayOfWeek.Monday)
            {
                var alignedStart = MondayOnOrAfter(start);
                delta += (alignedStart.Date - start.Date).Days;
                start = alignedStart;
            }
            end = isPinnedEnd ? end.AddDays(delta) : (IsExamEvent(item.EventType) ? NthBusinessDayAfter(start, duration - 1, holidays) : start.AddDays(duration - 1));
            var prohibitionText = string.IsNullOrWhiteSpace(prohibited.Value.Description)
                ? "yasak aralık"
                : prohibited.Value.Description.Trim();
            warnings.Add(new("PROHIBITION_SHIFT", "Warning", $"{item.Title} {prohibitionText} ({FormatDateRange(prohibited.Value.Start, prohibited.Value.End)}) nedeniyle {delta} gün kaydırıldı. Yeni tarih: {FormatDateRange(start, end)}.", EventId(academicYear, semester, item.Key)));
        }

        if (item.AvoidHolidayConflict && IsCriticalEvent(item.EventType) && !RequiresSequenceAwareHolidayPlacement(item.EventType))
        {
            var attempts = 0;
            var shiftReasons = new List<string>();
            var shiftBackwardForWeekend = ShouldShiftBackwardForWeekend(item.EventType, start, holidays);
            while (attempts < 14)
            {
                var conflicts = FindBlockingConflictDescriptions(start, end, item.EventType, holidays);
                if (conflicts.Count == 0)
                    break;

                foreach (var conflict in conflicts)
                {
                    if (!shiftReasons.Any(existing => string.Equals(existing, conflict, StringComparison.OrdinalIgnoreCase)))
                        shiftReasons.Add(conflict);
                }

                var shiftDays = ResolveConflictShiftDays(item.EventType, shiftBackwardForWeekend);
                start = shiftBackwardForWeekend ? start.AddDays(-shiftDays) : start.AddDays(shiftDays);
                end = isPinnedEnd
                    ? (shiftBackwardForWeekend ? end.AddDays(-shiftDays) : end.AddDays(shiftDays))
                    : (IsExamEvent(item.EventType) ? NthBusinessDayAfter(start, duration - 1, holidays) : start.AddDays(duration - 1));
                attempts += shiftDays;
            }

            if (attempts > 0)
            {
                var reasonText = shiftReasons.Count > 0
                    ? string.Join(", ", shiftReasons)
                    : "takvim çakışması";
                var directionText = shiftBackwardForWeekend ? "geriye" : "ileri";
                warnings.Add(new("HOLIDAY_SHIFT", "Warning", $"{item.Title} {reasonText} nedeniyle {attempts} gün {directionText} kaydırıldı. Yeni tarih: {FormatDateRange(start, end)}.", EventId(academicYear, semester, item.Key)));
            }

            if (attempts >= 14)
                return BuildEvent(academicYear, semester, item, chainCode, start, end, true, "Otomatik yerleşim başarısız.");
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
                holiday.IsEstimated ? "Dini gün tarihi Hicri fallback ile tahmini hesaplandı." : holiday.Source);
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

    private static CalendarTemplateItemDto ApplyDurationOverride(CalendarTemplateItemDto item, SemesterType semester, Dictionary<string, int>? overrides)
    {
        if (overrides == null)
            return item;

        if (overrides.TryGetValue($"{item.Key}:{semester}", out var semesterDays) && semesterDays > 0)
            return item with { DefaultDurationDays = semesterDays };

        return overrides.TryGetValue(item.Key, out var days) && days > 0
            ? item with { DefaultDurationDays = days }
            : item;
    }

    private static int ApplyDurationConstraint(int currentDuration, CalendarTemplateItemDto item, string chainCode, SemesterType semester, IReadOnlyList<CalendarConstraintInputDto> constraints)
    {
        var match = constraints.Where(c => c.ConstraintType == CalendarConstraintType.Duration && Matches(c, item.EventType, chainCode, item.Key, semester))
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

    private static (DateTime Start, DateTime? End)? FindPinConstraint(CalendarTemplateItemDto item, string chainCode, SemesterType semester, IReadOnlyList<CalendarConstraintInputDto> constraints)
    {
        var match = constraints.Where(c => c.ConstraintType == CalendarConstraintType.Pin && Matches(c, item.EventType, chainCode, item.Key, semester))
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

    private static (DateTime Start, DateTime End, string? Description)? FindProhibitionOverlap(DateTime start, DateTime end, IReadOnlyList<CalendarConstraintInputDto> constraints)
    {
        foreach (var constraint in constraints.Where(c => c.ConstraintType == CalendarConstraintType.Prohibition).OrderBy(c => c.Priority))
        {
            try
            {
                using var doc = JsonDocument.Parse(constraint.ParameterJson);
                var from = ReadDate(doc.RootElement, "from");
                var to = ReadDate(doc.RootElement, "to");
                if (from.HasValue && to.HasValue && DateRangesOverlap(start, end, from.Value, to.Value))
                    return (EnsureUtcDate(from.Value), EnsureUtcDate(to.Value), constraint.Description);
            }
            catch (JsonException) { }
        }
        return null;
    }
}
