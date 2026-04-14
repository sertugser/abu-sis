using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CalendarGenerator.DTOs;
using SIS.Application.CalendarGenerator.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.CalendarGenerator;

public class HolidayCalculatorService : IHolidayCalculatorService
{
    private readonly AppDbContext _context;
    private readonly ICalendarTemplateService _templateService;

    public HolidayCalculatorService(AppDbContext context, ICalendarTemplateService templateService)
    {
        _context = context;
        _templateService = templateService;
    }

    public async Task<IReadOnlyList<ResolvedHolidayDto>> ResolveHolidaysAsync(int startYear, int endYear, CancellationToken ct = default)
    {
        await _templateService.EnsureDefaultsAsync(ct);

        var definitions = await _context.HolidayDefinitions
            .AsNoTracking()
            .Where(h => h.IsActive)
            .ToListAsync(ct);

        var result = new List<ResolvedHolidayDto>();

        foreach (var year in Enumerable.Range(startYear, endYear - startYear + 1))
        {
            foreach (var definition in definitions)
            {
                var resolved = Resolve(definition, year);
                if (resolved != null)
                    result.Add(resolved);
            }
        }

        return result
            .DistinctBy(h => new { h.Name, Start = h.StartDate.Date, End = h.EndDate.Date })
            .OrderBy(h => h.StartDate)
            .ToList();
    }

    private static ResolvedHolidayDto? Resolve(HolidayDefinition definition, int year)
    {
        var overrides = CalendarTemplateService.DeserializeOverrides(definition.OverridesJson);
        var matchedOverride = overrides.FirstOrDefault(o => o.Year == year);
        if (matchedOverride != null)
        {
            return new ResolvedHolidayDto(
                definition.Name,
                definition.EnglishName,
                definition.Type,
                EnsureUtcDate(matchedOverride.StartDate),
                EnsureUtcDate(matchedOverride.EndDate),
                definition.DurationDays,
                definition.IsOfficialHoliday,
                definition.BlocksAcademicEvents,
                matchedOverride.IsEstimated,
                matchedOverride.Source ?? definition.Source);
        }

        if (definition.Type is HolidayType.Fixed or HolidayType.ImportantDay)
        {
            if (!definition.FixedMonth.HasValue || !definition.FixedDay.HasValue)
                return null;

            var start = Utc(year, definition.FixedMonth.Value, definition.FixedDay.Value);
            var end = definition.DurationDays switch
            {
                > 1m and < 2m => start.AddDays(1),
                >= 2m => start.AddDays((double)Math.Ceiling(definition.DurationDays) - 1),
                _ => start
            };

            return new ResolvedHolidayDto(
                definition.Name,
                definition.EnglishName,
                definition.Type,
                start,
                end,
                definition.DurationDays,
                definition.IsOfficialHoliday,
                definition.BlocksAcademicEvents,
                false,
                definition.Source);
        }

        if (definition.Type == HolidayType.HijriCalculated)
        {
            if (!definition.HijriMonth.HasValue || !definition.HijriDay.HasValue)
                return null;

            var firstFeastDay = ConvertHijriToGregorianApprox(year, definition.HijriMonth.Value, definition.HijriDay.Value);
            if (firstFeastDay == null)
                return null;

            var start = definition.IsHalfDayStart ? firstFeastDay.Value.AddDays(-1) : firstFeastDay.Value;
            var totalDays = (int)Math.Ceiling(definition.DurationDays);
            var end = start.AddDays(totalDays - 1);

            return new ResolvedHolidayDto(
                definition.Name,
                definition.EnglishName,
                definition.Type,
                start,
                end,
                definition.DurationDays,
                definition.IsOfficialHoliday,
                definition.BlocksAcademicEvents,
                true,
                $"{definition.Source}:fallback");
        }

        return null;
    }

    private static DateTime? ConvertHijriToGregorianApprox(int gregorianYear, int hijriMonth, int hijriDay)
    {
        var calendar = new UmAlQuraCalendar();

        // A Gregorian year overlaps two Hijri years. Try both likely years and choose
        // the conversion that lands inside the requested Gregorian year.
        var approximateHijriYear = (int)Math.Floor((gregorianYear - 622) * 33m / 32m);
        foreach (var hijriYear in new[] { approximateHijriYear - 1, approximateHijriYear, approximateHijriYear + 1 })
        {
            try
            {
                var date = calendar.ToDateTime(hijriYear, hijriMonth, hijriDay, 0, 0, 0, 0);
                if (date.Year == gregorianYear)
                    return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Keep trying nearby Hijri years.
            }
        }

        return null;
    }

    private static DateTime EnsureUtcDate(DateTime date) =>
        DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
