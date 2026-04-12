using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class BlockCombination
{
    public BlockAssignment? TheoryBlock { get; init; }
    public BlockAssignment? LabBlock { get; init; }
}

public sealed class CandidateGenerator
{
    private const int MaxCombinations = 200;
    private const int MaxWindowsPerDay = 5;
    private const int MaxClassroomsPerType = 10;

    /// <summary>
    /// Classroom types considered compatible for theory/general teaching.
    /// Lab types require exact match.
    /// </summary>
    private static readonly HashSet<ClassroomType> GeneralTeachingTypes = new()
    {
        ClassroomType.Lecture,
        ClassroomType.Seminar,
        ClassroomType.Amphitheater
    };

    public List<BlockCombination> Generate(
        SectionSnapshot section,
        MutableScheduleState state,
        List<Classroom> allClassrooms,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        int splitThreshold,
        HashSet<int> facultyClassroomIds,
        HashSet<BlockAssignment>? excludeBlocks = null)
    {
        var hasTheory = section.TheoryHours > 0;
        var hasLab = section.LabHours > 0;

        // Sort days by least loaded first (exclude weekends)
        var sortedDays = timeSlotsByDay.Keys
            .Where(d => d != DayOfWeek.Saturday && d != DayOfWeek.Sunday)
            .OrderBy(d => state.GetDaySectionCount(d))
            .ThenBy(d => d)
            .ToList();

        if (sortedDays.Count == 0)
            return new List<BlockCombination>();

        if (hasTheory && hasLab)
            return GenerateMixed(section, state, allClassrooms, timeSlotsByDay, sortedDays,
                splitThreshold, facultyClassroomIds, excludeBlocks);

        if (hasTheory)
            return GenerateSingleBlock(
                BlockType.Theory,
                section.TheoryHours,
                section.TheoryClassroomType ?? ClassroomType.Lecture,
                section, state, allClassrooms, timeSlotsByDay, sortedDays,
                facultyClassroomIds, excludeBlocks);

        if (hasLab)
            return GenerateSingleBlock(
                BlockType.Lab,
                section.LabHours,
                section.LabClassroomType ?? ClassroomType.Lab,
                section, state, allClassrooms, timeSlotsByDay, sortedDays,
                facultyClassroomIds, excludeBlocks);

        return new List<BlockCombination>();
    }

    private List<BlockCombination> GenerateSingleBlock(
        BlockType blockType,
        int requiredHours,
        ClassroomType requiredClassroomType,
        SectionSnapshot section,
        MutableScheduleState state,
        List<Classroom> allClassrooms,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        List<DayOfWeek> sortedDays,
        HashSet<int> facultyClassroomIds,
        HashSet<BlockAssignment>? excludeBlocks)
    {
        var classrooms = FilterClassrooms(allClassrooms, requiredClassroomType, section.Capacity, facultyClassroomIds);
        var blocks = FindBlocks(blockType, requiredHours, classrooms, state, timeSlotsByDay, sortedDays, excludeBlocks);

        return blocks
            .Take(MaxCombinations)
            .Select(b => blockType == BlockType.Theory
                ? new BlockCombination { TheoryBlock = b }
                : new BlockCombination { LabBlock = b })
            .ToList();
    }

    private List<BlockCombination> GenerateMixed(
        SectionSnapshot section,
        MutableScheduleState state,
        List<Classroom> allClassrooms,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        List<DayOfWeek> sortedDays,
        int splitThreshold,
        HashSet<int> facultyClassroomIds,
        HashSet<BlockAssignment>? excludeBlocks)
    {
        var theoryType = section.TheoryClassroomType ?? ClassroomType.Lecture;
        var labType = section.LabClassroomType ?? ClassroomType.Lab;

        var theoryClassrooms = FilterClassrooms(allClassrooms, theoryType, section.Capacity, facultyClassroomIds);
        var labClassrooms = FilterClassrooms(allClassrooms, labType, section.Capacity, facultyClassroomIds);

        var theoryBlocks = FindBlocks(BlockType.Theory, section.TheoryHours, theoryClassrooms, state, timeSlotsByDay, sortedDays, excludeBlocks);
        var labBlocks = FindBlocks(BlockType.Lab, section.LabHours, labClassrooms, state, timeSlotsByDay, sortedDays, excludeBlocks);

        var totalHours = section.TheoryHours + section.LabHours;
        var mustSplitDays = totalHours > splitThreshold;

        var results = new List<BlockCombination>();

        foreach (var theory in theoryBlocks)
        {
            foreach (var lab in labBlocks)
            {
                if (mustSplitDays && theory.Day == lab.Day)
                    continue;

                if (!mustSplitDays && theory.Day == lab.Day)
                {
                    // Same day allowed but no time overlap
                    if (HasTimeOverlap(theory, lab))
                        continue;
                }

                results.Add(new BlockCombination
                {
                    TheoryBlock = theory,
                    LabBlock = lab
                });

                if (results.Count >= MaxCombinations)
                    return results;
            }
        }

        return results;
    }

    private static List<Classroom> FilterClassrooms(
        List<Classroom> allClassrooms,
        ClassroomType requiredType,
        int requiredCapacity,
        HashSet<int> facultyClassroomIds)
    {
        // For general teaching types (Lecture, Seminar, Amphitheater),
        // accept any general teaching classroom. For specific lab types,
        // require exact match.
        var isGeneralType = GeneralTeachingTypes.Contains(requiredType);

        return allClassrooms
            .Where(c => facultyClassroomIds.Contains(c.Id))
            .Where(c => isGeneralType
                ? GeneralTeachingTypes.Contains(c.Type)
                : c.Type == requiredType)
            .Where(c => c.Capacity >= requiredCapacity)
            .OrderBy(c => c.Type == requiredType ? 0 : 1) // prefer exact match first
            .ThenBy(c => c.Capacity) // then smallest sufficient
            .Take(MaxClassroomsPerType)
            .ToList();
    }

    private static List<BlockAssignment> FindBlocks(
        BlockType blockType,
        int requiredHours,
        List<Classroom> classrooms,
        MutableScheduleState state,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        List<DayOfWeek> sortedDays,
        HashSet<BlockAssignment>? excludeBlocks)
    {
        var results = new List<BlockAssignment>();

        foreach (var classroom in classrooms)
        {
            foreach (var day in sortedDays)
            {
                if (!timeSlotsByDay.TryGetValue(day, out var daySlots) || daySlots.Count == 0)
                    continue;

                var sorted = daySlots.OrderBy(s => s.StartTime).ToList();
                var occupancy = state.GetClassroomOccupancy(classroom.Id, day);
                var windowsFound = 0;

                for (var i = 0; i <= sorted.Count - requiredHours; i++)
                {
                    if (windowsFound >= MaxWindowsPerDay)
                        break;

                    // Check if requiredHours consecutive slots starting at i are valid
                    var valid = true;
                    for (var j = 0; j < requiredHours - 1; j++)
                    {
                        var current = sorted[i + j];
                        var next = sorted[i + j + 1];
                        var gap = next.StartTime - current.EndTime;

                        // Consecutive check: next slot starts within 10 minutes of current ending
                        if (gap.TotalMinutes > 10 || gap.TotalMinutes < 0)
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (!valid)
                        continue;

                    var windowStart = sorted[i].StartTime;
                    var windowEnd = sorted[i + requiredHours - 1].EndTime;

                    // Check no overlap with existing occupancy
                    var occupied = false;
                    foreach (var (_, start, end, _) in occupancy)
                    {
                        if (windowStart < end && windowEnd > start)
                        {
                            occupied = true;
                            break;
                        }
                    }

                    if (occupied)
                        continue;

                    var block = new BlockAssignment(blockType, classroom.Id, day, windowStart, windowEnd);

                    // Check exclude list
                    if (excludeBlocks != null && excludeBlocks.Contains(block))
                        continue;

                    results.Add(block);
                    windowsFound++;
                }
            }
        }

        return results;
    }

    private static bool HasTimeOverlap(BlockAssignment a, BlockAssignment b)
    {
        return a.StartTime < b.EndTime && a.EndTime > b.StartTime;
    }
}
