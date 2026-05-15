using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Services;

public static class SchedulingHelpers
{
    public static List<TimeBlock> GetOverlapSlots(IReadOnlyList<TimeBlock> firstSchedule, IReadOnlyList<TimeBlock> secondSchedule)
    {
        var overlaps = new List<TimeBlock>();

        foreach (var first in firstSchedule)
        {
            foreach (var second in secondSchedule)
            {
                if (!TryGetOverlap(first, second, out var overlap))
                {
                    continue;
                }

                overlaps.Add(overlap);
            }
        }

        return overlaps
            .GroupBy(slot => BuildSlotSignature(new[] { slot }))
            .Select(group => group.First())
            .OrderBy(slot => slot.Day)
            .ThenBy(slot => slot.StartTime)
            .ToList();
    }

    public static bool TryGetOverlap(TimeBlock first, TimeBlock second, out TimeBlock overlap)
    {
        overlap = default!;

        if (first.Day != second.Day)
        {
            return false;
        }

        var start = first.StartTime > second.StartTime ? first.StartTime : second.StartTime;
        var end = first.EndTime < second.EndTime ? first.EndTime : second.EndTime;

        if (start >= end)
        {
            return false;
        }

        overlap = new TimeBlock
        {
            Day = first.Day,
            StartTime = start,
            EndTime = end
        };

        return true;
    }

    public static bool SlotsOverlap(TimeBlock first, TimeBlock second) =>
        first.Day == second.Day
        && first.StartTime < second.EndTime
        && second.StartTime < first.EndTime;

    public static int ResolveAssignedTimeSlotCount(IReadOnlyList<TimeBlock> slots) =>
        slots.Sum(slot => Math.Max(1, (int)Math.Round((slot.EndTime - slot.StartTime).TotalMinutes / 60.0, MidpointRounding.AwayFromZero)));

    public static decimal ResolveWeeklyHours(IReadOnlyList<TimeBlock> slots, Course course)
    {
        if (slots.Count > 0)
        {
            var minutes = slots.Sum(slot => (decimal)(slot.EndTime - slot.StartTime).TotalMinutes);
            return minutes / 60m;
        }

        var catalogHours = course.TheoryHours + course.LabHours;
        return catalogHours > 0 ? catalogHours : 0m;
    }

    public static string GetDayOfWeekName(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday => "Pazartesi",
            DayOfWeek.Tuesday => "Salı",
            DayOfWeek.Wednesday => "Çarşamba",
            DayOfWeek.Thursday => "Perşembe",
            DayOfWeek.Friday => "Cuma",
            DayOfWeek.Saturday => "Cumartesi",
            DayOfWeek.Sunday => "Pazar",
            _ => day.ToString()
        };
    }

    public static string GetDayName(int day)
    {
        return day switch
        {
            0 => "Pazartesi",
            1 => "Salı",
            2 => "Çarşamba",
            3 => "Perşembe",
            4 => "Cuma",
            _ => "Bilinmeyen"
        };
    }

    // Used by GetOverlapSlots — needs to be accessible here
    private static string BuildSlotSignature(IEnumerable<TimeBlock> slots) =>
        string.Join("|", slots
            .OrderBy(slot => slot.Day)
            .ThenBy(slot => slot.StartTime)
            .Select(slot => $"{(int)slot.Day}:{slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm}"));
}
