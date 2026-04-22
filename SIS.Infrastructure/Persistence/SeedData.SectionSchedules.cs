using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    private static async Task SeedSectionSchedulesAsync(AppDbContext context)
    {
        var now = DateTime.UtcNow;
        var activeTerm = await context.AcademicTerms
            .FirstOrDefaultAsync(term => !term.IsCancelled && term.TermStart <= now && term.TermEnd >= now);

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

        // Load TimeSlots for this term
        var timeSlots = await context.TimeSlots
            .Where(ts => ts.TermId == activeTerm.Id)
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.SlotOrder)
            .ToListAsync();

        if (timeSlots.Count == 0)
            return;

        // Track occupancy using (DayOfWeek, SlotOrder) tuples
        var instructorOccupancy = new Dictionary<int, List<(DayOfWeek Day, TimeOnly Start, TimeOnly End)>>();
        var classroomOccupancy = new Dictionary<int, List<(DayOfWeek Day, TimeOnly Start, TimeOnly End)>>();

        // Check existing SectionSchedule records
        var existingSchedules = await context.SectionSchedules
            .Include(ss => ss.TimeSlot)
            .Where(ss => ss.CourseSection.TermId == activeTerm.Id)
            .ToListAsync();

        foreach (var ss in existingSchedules)
        {
            var block = (ss.TimeSlot.DayOfWeek, ss.TimeSlot.StartTime, ss.TimeSlot.EndTime);

            var sectionForSs = sections.FirstOrDefault(s => s.Id == ss.CourseSectionId);
            if (sectionForSs?.InstructorId != null)
            {
                if (!instructorOccupancy.TryGetValue(sectionForSs.InstructorId.Value, out var iSlots))
                {
                    iSlots = new();
                    instructorOccupancy[sectionForSs.InstructorId.Value] = iSlots;
                }
                iSlots.Add(block);
            }

            if (!classroomOccupancy.TryGetValue(ss.ClassroomId, out var cSlots))
            {
                cSlots = new();
                classroomOccupancy[ss.ClassroomId] = cSlots;
            }
            cSlots.Add(block);
        }

        var scheduledSectionIds = existingSchedules
            .Select(ss => ss.CourseSectionId)
            .Distinct()
            .ToHashSet();

        var changed = false;

        foreach (var section in sections.Where(s => !scheduledSectionIds.Contains(s.Id) && s.ClassroomId == null))
        {
            var preferredClassrooms = classrooms
                .Where(classroom => classroom.Capacity >= Math.Max(section.Capacity, 1))
                .OrderBy(classroom => classroom.Capacity - Math.Max(section.Capacity, 1))
                .ThenBy(classroom => classroom.Name)
                .ToList();

            if (preferredClassrooms.Count == 0)
                preferredClassrooms = classrooms;

            var weeklyHours = Math.Max(1, section.Course.TheoryHours + section.Course.LabHours);
            var slotCombinations = BuildSeedTimeSlotCombinations(weeklyHours, timeSlots);
            if (slotCombinations.Count == 0)
                continue;

            var assigned = false;
            foreach (var classroom in preferredClassrooms)
            {
                foreach (var candidateSlots in slotCombinations)
                {
                    var candidateBlocks = candidateSlots
                        .Select(ts => (ts.DayOfWeek, ts.StartTime, ts.EndTime))
                        .ToList();

                    if (section.InstructorId.HasValue
                        && instructorOccupancy.TryGetValue(section.InstructorId.Value, out var instrSlots)
                        && HasAnyBlockOverlap(candidateBlocks, instrSlots))
                    {
                        continue;
                    }

                    if (classroomOccupancy.TryGetValue(classroom.Id, out var classSlots)
                        && HasAnyBlockOverlap(candidateBlocks, classSlots))
                    {
                        continue;
                    }

                    // Apply assignment
                    section.ClassroomId = classroom.Id;

                    foreach (var ts in candidateSlots)
                    {
                        context.SectionSchedules.Add(new SectionSchedule
                        {
                            CourseSectionId = section.Id,
                            TimeSlotId = ts.Id,
                            ClassroomId = classroom.Id
                        });
                    }

                    changed = true;

                    // Update occupancy tracking
                    if (section.InstructorId.HasValue)
                    {
                        if (!instructorOccupancy.TryGetValue(section.InstructorId.Value, out var iSlots))
                        {
                            iSlots = new();
                            instructorOccupancy[section.InstructorId.Value] = iSlots;
                        }
                        iSlots.AddRange(candidateBlocks);
                    }

                    if (!classroomOccupancy.TryGetValue(classroom.Id, out var cOccupied))
                    {
                        cOccupied = new();
                        classroomOccupancy[classroom.Id] = cOccupied;
                    }
                    cOccupied.AddRange(candidateBlocks);

                    assigned = true;
                    break;
                }
                if (assigned) break;
            }
        }

        if (changed)
            await context.SaveChangesAsync();
    }

    /// <summary>
    /// Builds combinations of TimeSlot groups covering the required weekly hours.
    /// Returns lists of TimeSlot entities that form valid non-overlapping combinations.
    /// </summary>
    private static List<List<TimeSlot>> BuildSeedTimeSlotCombinations(int weeklyHours, List<TimeSlot> timeSlots)
    {
        // Determine block durations needed (in minutes)
        var blockMinutes = weeklyHours switch
        {
            <= 1 => new[] { 60 },
            2 => new[] { 120 },
            3 => new[] { 180 },
            4 => new[] { 120, 120 },
            5 => new[] { 180, 120 },
            _ => new[] { 180, 180 }
        };

        // Group time slots by day
        var slotsByDay = timeSlots
            .GroupBy(ts => ts.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.OrderBy(ts => ts.SlotOrder).ToList());

        // For each block, find all possible consecutive windows across all days
        var perBlockCandidates = blockMinutes
            .Select(minutes =>
            {
                var candidates = new List<List<TimeSlot>>();
                foreach (var daySlots in slotsByDay.Values)
                {
                    // Find consecutive windows of the required duration
                    for (int i = 0; i < daySlots.Count; i++)
                    {
                        var window = new List<TimeSlot> { daySlots[i] };
                        var totalMinutes = (daySlots[i].EndTime - daySlots[i].StartTime).TotalMinutes;

                        for (int j = i + 1; j < daySlots.Count && totalMinutes < minutes; j++)
                        {
                            // Check contiguous (gap <= 10 min)
                            if (daySlots[j].StartTime <= window[^1].EndTime.AddMinutes(10))
                            {
                                window.Add(daySlots[j]);
                                totalMinutes += (daySlots[j].EndTime - daySlots[j].StartTime).TotalMinutes;
                            }
                            else break;
                        }

                        if (totalMinutes >= minutes)
                            candidates.Add(window);
                    }
                }
                return candidates;
            })
            .ToList();

        if (perBlockCandidates.Any(c => c.Count == 0))
            return new List<List<TimeSlot>>();

        // Build combinations ensuring no overlap and different days for multi-block
        var results = new List<List<TimeSlot>>();

        void Build(int depth, List<TimeSlot> current)
        {
            if (results.Count >= 100) return;
            if (depth == perBlockCandidates.Count)
            {
                results.Add(current.OrderBy(ts => ts.DayOfWeek).ThenBy(ts => ts.SlotOrder).ToList());
                return;
            }

            foreach (var candidate in perBlockCandidates[depth])
            {
                // Ensure no day overlap with already selected slots
                if (current.Any(existing => candidate.Any(c => c.DayOfWeek == existing.DayOfWeek)))
                    continue;

                current.AddRange(candidate);
                Build(depth + 1, current);
                current.RemoveRange(current.Count - candidate.Count, candidate.Count);
            }
        }

        Build(0, new List<TimeSlot>());
        return results;
    }

    private static bool HasAnyBlockOverlap(
        IEnumerable<(DayOfWeek Day, TimeOnly Start, TimeOnly End)> candidateBlocks,
        IEnumerable<(DayOfWeek Day, TimeOnly Start, TimeOnly End)> existingBlocks) =>
        candidateBlocks.Any(candidate => existingBlocks.Any(existing =>
            candidate.Day == existing.Day
            && candidate.Start < existing.End
            && existing.Start < candidate.End));
}
