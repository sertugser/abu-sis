using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.ValueObjects;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    private static readonly JsonSerializerOptions ScheduleSeedJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static async Task SeedSectionSchedulesAsync(AppDbContext context)
    {
        var activeTerm = await context.AcademicTerms
            .FirstOrDefaultAsync(term => term.IsActive);

        if (activeTerm == null)
            return;

        var sections = await context.CourseSections
            .Include(section => section.Course)
            .Include(section => section.Classroom)
            .Include(section => section.Instructor)
            .Where(section => section.TermId == activeTerm.Id && section.IsActive)
            .OrderBy(section => section.Course.Code)
            .ThenBy(section => section.Id)
            .ToListAsync();

        if (sections.Count == 0)
            return;

        var classrooms = await context.Classrooms
            .OrderBy(classroom => classroom.Capacity)
            .ThenBy(classroom => classroom.Name)
            .ToListAsync();

        if (classrooms.Count == 0)
            return;

        var instructorOccupancy = new Dictionary<int, List<ScheduleSlot>>();
        var classroomOccupancy = new Dictionary<int, List<ScheduleSlot>>();

        foreach (var section in sections)
        {
            var existingSlots = ParseSchedule(section.ScheduleJson);
            if (existingSlots.Count == 0)
                continue;

            if (section.InstructorId.HasValue)
            {
                if (!instructorOccupancy.TryGetValue(section.InstructorId.Value, out var slots))
                {
                    slots = new List<ScheduleSlot>();
                    instructorOccupancy[section.InstructorId.Value] = slots;
                }

                slots.AddRange(existingSlots);
            }

            if (section.ClassroomId.HasValue)
            {
                if (!classroomOccupancy.TryGetValue(section.ClassroomId.Value, out var slots))
                {
                    slots = new List<ScheduleSlot>();
                    classroomOccupancy[section.ClassroomId.Value] = slots;
                }

                slots.AddRange(existingSlots);
            }
        }

        var changed = false;

        foreach (var section in sections.Where(currentSection => NeedsScheduleBackfill(currentSection)))
        {
            var preferredClassrooms = classrooms
                .Where(classroom => classroom.Capacity >= Math.Max(section.Capacity, 1))
                .OrderBy(classroom => classroom.Capacity - Math.Max(section.Capacity, 1))
                .ThenBy(classroom => classroom.Name)
                .ToList();

            if (preferredClassrooms.Count == 0)
                preferredClassrooms = classrooms;

            var slotDurations = ResolveSeedSlotDurations(section);
            var combinations = BuildSeedSlotCombinations(slotDurations);
            if (combinations.Count == 0)
                continue;

            var selectedSlots = combinations[0];
            var selectedClassroom = preferredClassrooms[0];

            var assigned = false;
            foreach (var classroom in preferredClassrooms)
            {
                foreach (var candidateSlots in combinations)
                {
                    if (section.InstructorId.HasValue
                        && instructorOccupancy.TryGetValue(section.InstructorId.Value, out var instructorSlots)
                        && HasAnyOverlap(candidateSlots, instructorSlots))
                    {
                        continue;
                    }

                    if (classroomOccupancy.TryGetValue(classroom.Id, out var classroomSlots)
                        && HasAnyOverlap(candidateSlots, classroomSlots))
                    {
                        continue;
                    }

                    selectedClassroom = classroom;
                    selectedSlots = candidateSlots;
                    assigned = true;
                    break;
                }

                if (assigned)
                    break;
            }

            section.ClassroomId = selectedClassroom.Id;
            section.ScheduleJson = JsonSerializer.Serialize(selectedSlots, ScheduleSeedJsonOptions);
            changed = true;

            if (section.InstructorId.HasValue)
            {
                if (!instructorOccupancy.TryGetValue(section.InstructorId.Value, out var slots))
                {
                    slots = new List<ScheduleSlot>();
                    instructorOccupancy[section.InstructorId.Value] = slots;
                }

                slots.AddRange(selectedSlots);
            }

            if (!classroomOccupancy.TryGetValue(selectedClassroom.Id, out var occupiedSlots))
            {
                occupiedSlots = new List<ScheduleSlot>();
                classroomOccupancy[selectedClassroom.Id] = occupiedSlots;
            }

            occupiedSlots.AddRange(selectedSlots);
        }

        if (changed)
            await context.SaveChangesAsync();
    }

    private static bool NeedsScheduleBackfill(CourseSection section) =>
        section.ClassroomId == null
        || string.IsNullOrWhiteSpace(section.ScheduleJson)
        || section.ScheduleJson == "[]";

    private static List<ScheduleSlot> ParseSchedule(string scheduleJson)
    {
        if (string.IsNullOrWhiteSpace(scheduleJson) || scheduleJson == "[]")
            return new List<ScheduleSlot>();

        try
        {
            return JsonSerializer.Deserialize<List<ScheduleSlot>>(scheduleJson, ScheduleSeedJsonOptions) ?? new List<ScheduleSlot>();
        }
        catch
        {
            return new List<ScheduleSlot>();
        }
    }

    private static List<int> ResolveSeedSlotDurations(CourseSection section)
    {
        var weeklyHours = Math.Max(1, section.Course.TheoryHours + section.Course.LabHours);

        return weeklyHours switch
        {
            <= 1 => [60],
            2 => [120],
            3 => [180],
            4 => [120, 120],
            5 => [180, 120],
            _ => [180, 180]
        };
    }

    private static List<List<ScheduleSlot>> BuildSeedSlotCombinations(IReadOnlyList<int> durations)
    {
        var availableSlots = BuildSeedCandidateSlots();
        var combinations = new List<List<ScheduleSlot>>();

        void Build(int depth, int startIndex, List<ScheduleSlot> current)
        {
            if (depth == durations.Count)
            {
                combinations.Add(current
                    .OrderBy(slot => slot.Day)
                    .ThenBy(slot => slot.StartTime)
                    .ToList());
                return;
            }

            for (var index = startIndex; index < availableSlots.Count; index++)
            {
                var candidate = availableSlots[index];
                var duration = (candidate.EndTime.ToTimeSpan() - candidate.StartTime.ToTimeSpan()).TotalMinutes;
                if (duration != durations[depth])
                    continue;

                if (current.Any(existing => existing.Day == candidate.Day))
                    continue;

                current.Add(candidate);
                Build(depth + 1, index + 1, current);
                current.RemoveAt(current.Count - 1);
            }
        }

        Build(0, 0, new List<ScheduleSlot>());
        return combinations;
    }

    private static List<ScheduleSlot> BuildSeedCandidateSlots()
    {
        var days = new[]
        {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        };

        var slots = new List<ScheduleSlot>();
        foreach (var day in days)
        {
            slots.Add(new ScheduleSlot { Day = day, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0) });
            slots.Add(new ScheduleSlot { Day = day, StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(16, 0) });
            slots.Add(new ScheduleSlot { Day = day, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 0) });
            slots.Add(new ScheduleSlot { Day = day, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(12, 0) });
            slots.Add(new ScheduleSlot { Day = day, StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(15, 0) });
            slots.Add(new ScheduleSlot { Day = day, StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(16, 0) });
            slots.Add(new ScheduleSlot { Day = day, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0) });
            slots.Add(new ScheduleSlot { Day = day, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0) });
            slots.Add(new ScheduleSlot { Day = day, StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(12, 0) });
            slots.Add(new ScheduleSlot { Day = day, StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(14, 0) });
            slots.Add(new ScheduleSlot { Day = day, StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(15, 0) });
            slots.Add(new ScheduleSlot { Day = day, StartTime = new TimeOnly(15, 0), EndTime = new TimeOnly(16, 0) });
        }

        return slots;
    }

    private static bool HasAnyOverlap(IEnumerable<ScheduleSlot> candidateSlots, IEnumerable<ScheduleSlot> existingSlots) =>
        candidateSlots.Any(candidate => existingSlots.Any(existing =>
            candidate.Day == existing.Day
            && candidate.StartTime < existing.EndTime
            && existing.StartTime < candidate.EndTime));
}
