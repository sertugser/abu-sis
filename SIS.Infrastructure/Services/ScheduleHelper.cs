using SIS.Application.FacultyPlanning.DTOs;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Lightweight time-block class used internally for schedule overlap checks
/// and slot combination logic. Replaces the former ScheduleSlot value object.
/// </summary>
public sealed class TimeBlock
{
    public DayOfWeek Day { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int? ClassroomId { get; set; }
}

/// <summary>
/// Converts SectionSchedule records (from database) to TimeBlock list
/// and formats schedule summaries for display.
/// </summary>
public static class ScheduleHelper
{
    /// <summary>
    /// Converts SectionSchedule records to internal TimeBlock list.
    /// The SectionSchedule records must have their TimeSlot navigation property loaded.
    /// </summary>
    internal static List<TimeBlock> ToTimeBlocks(IEnumerable<SectionSchedule> schedules)
    {
        return schedules
            .Select(ss => new TimeBlock
            {
                Day = ss.TimeSlot.DayOfWeek,
                StartTime = ss.TimeSlot.StartTime,
                EndTime = ss.TimeSlot.EndTime,
                ClassroomId = ss.ClassroomId
            })
            .OrderBy(s => s.Day)
            .ThenBy(s => s.StartTime)
            .ToList();
    }

    /// <summary>
    /// Formats SectionSchedule records into a human-readable Turkish schedule string.
    /// Example: "Pazartesi 09:00-09:50, Carsamba 13:00-13:50"
    /// </summary>
    public static string FormatSchedule(IEnumerable<SectionSchedule> schedules)
    {
        var slots = schedules
            .OrderBy(ss => ss.TimeSlot.DayOfWeek)
            .ThenBy(ss => ss.TimeSlot.StartTime)
            .ToList();

        if (slots.Count == 0)
            return string.Empty;

        // Group consecutive slots on the same day into ranges
        var ranges = new List<(DayOfWeek Day, TimeOnly Start, TimeOnly End)>();
        foreach (var slot in slots)
        {
            var day = slot.TimeSlot.DayOfWeek;
            var start = slot.TimeSlot.StartTime;
            var end = slot.TimeSlot.EndTime;

            if (ranges.Count > 0)
            {
                var last = ranges[^1];
                // Merge if same day and contiguous (last end == this start or within 10 min gap)
                if (last.Day == day && start <= last.End.AddMinutes(10))
                {
                    ranges[^1] = (day, last.Start, end > last.End ? end : last.End);
                    continue;
                }
            }
            ranges.Add((day, start, end));
        }

        return string.Join(", ", ranges.Select(r =>
            $"{DayOfWeekToTurkish(r.Day)} {r.Start:HH:mm}-{r.End:HH:mm}"));
    }

    /// <summary>
    /// Formats SectionSchedule records into a short summary. BlockType-aware:
    /// theory + lab on the same day are kept as separate ranges so the summary
    /// reflects the structural split (mixed courses).
    /// Example: "Pzt 09:00-09:50, Car 13:00-13:50"
    /// </summary>
    public static string FormatScheduleSummary(IEnumerable<SectionSchedule> schedules)
    {
        var blocks = BuildScheduleBlocks(schedules);
        if (blocks.Count == 0) return "Zamanlanmamis";
        return string.Join(", ", blocks.Select(b =>
            $"{DayOfWeekToTurkishShort((DayOfWeek)b.Day)} {b.StartTime:HH:mm}-{b.EndTime:HH:mm}"));
    }

    /// <summary>
    /// Converts SectionSchedule records into structured block list, merging consecutive
    /// slots ONLY when they share the same day, classroom, AND BlockType. A theory
    /// slot followed by a lab slot on the same day stays as two separate blocks.
    /// </summary>
    public static List<ScheduleBlockDto> BuildScheduleBlocks(IEnumerable<SectionSchedule> schedules)
    {
        var slots = schedules
            .OrderBy(ss => ss.TimeSlot.DayOfWeek)
            .ThenBy(ss => ss.TimeSlot.StartTime)
            .ToList();

        var blocks = new List<ScheduleBlockDto>();
        foreach (var slot in slots)
        {
            var day = slot.TimeSlot.DayOfWeek;
            var start = slot.TimeSlot.StartTime;
            var end = slot.TimeSlot.EndTime;
            var classroomId = slot.ClassroomId;
            var blockType = slot.BlockType;

            if (blocks.Count > 0)
            {
                var last = blocks[^1];
                // Merge contiguous slots only when day + classroom + block type all match.
                // Theory→Lab transitions on the same day stay split.
                if (last.Day == (int)day
                    && last.ClassroomId == classroomId
                    && last.BlockType == blockType
                    && start <= last.EndTime.AddMinutes(10))
                {
                    last.EndTime = end > last.EndTime ? end : last.EndTime;
                    continue;
                }
            }

            blocks.Add(new ScheduleBlockDto
            {
                Day = (int)day,
                StartTime = start,
                EndTime = end,
                BlockType = blockType,
                ClassroomId = classroomId,
                ClassroomName = slot.Classroom?.Name,
                ClassroomType = slot.Classroom?.Type.ToString(),
            });
        }
        return blocks;
    }

    private static string DayOfWeekToTurkish(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Sali",
        DayOfWeek.Wednesday => "Carsamba",
        DayOfWeek.Thursday => "Persembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        DayOfWeek.Sunday => "Pazar",
        _ => day.ToString()
    };

    private static string DayOfWeekToTurkishShort(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pzt",
        DayOfWeek.Tuesday => "Sal",
        DayOfWeek.Wednesday => "Car",
        DayOfWeek.Thursday => "Per",
        DayOfWeek.Friday => "Cum",
        DayOfWeek.Saturday => "Cmt",
        DayOfWeek.Sunday => "Paz",
        _ => day.ToString()
    };
}
