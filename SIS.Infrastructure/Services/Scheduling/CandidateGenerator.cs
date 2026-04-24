using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class BlockCombination
{
    public BlockAssignment? TheoryBlock { get; init; }
    public List<BlockAssignment> LabBlocks { get; init; } = new();
}

public sealed class CandidateGenerator
{
    private const int MaxCombinations = 200;
    private const int MaxWindowsPerDay = 5;
    private const int MaxClassroomsPerType = 10;

    /// <summary>
    /// Classroom types considered compatible for theory/general teaching.
    /// </summary>
    private static readonly HashSet<ClassroomType> GeneralTeachingTypes = new()
    {
        ClassroomType.Lecture,
        ClassroomType.Seminar,
        ClassroomType.Amphitheater
    };

    /// <summary>
    /// Lab/Computer types are interchangeable for scheduling purposes.
    /// </summary>
    private static readonly HashSet<ClassroomType> LabTeachingTypes = new()
    {
        ClassroomType.Lab,
        ClassroomType.Computer
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

        // Online dersler için özel akış: derslik iterasyonu yok, sadece zaman penceresi.
        // Weekend slotları da dahil — online için OnlineOnly günleri kullanılabilir.
        if (section.TeachingMethod == TeachingMethod.Online)
            return GenerateOnlineCandidates(section, state, timeSlotsByDay, splitThreshold, excludeBlocks);

        // Sort days by least loaded first. Hafta sonu slotları da dahil — bu slotlar
        // zaten AllowedMode=OnlineOnly damgalı gelir (seed'de WeekendOnlineOnly=true);
        // H17 FaceToFaceSlotRestrictionEvaluator OnlineOnly slota FaceToFace dersi bloklar.
        // Admin bir hafta sonu slotunu Both'a çevirirse yüz-yüze de düşebilir — tasarım gereği.
        var sortedDays = timeSlotsByDay.Keys
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
        if (blockType == BlockType.Lab)
        {
            return GenerateLabBlocks(
                requiredHours, requiredClassroomType, section, state,
                allClassrooms, timeSlotsByDay, sortedDays, facultyClassroomIds, excludeBlocks);
        }

        var classrooms = FilterClassrooms(allClassrooms, requiredClassroomType, section.Capacity, facultyClassroomIds);
        var blocks = FindBlocks(blockType, requiredHours, classrooms, state, timeSlotsByDay, sortedDays, excludeBlocks);

        return blocks
            .Take(MaxCombinations)
            .Select(b => new BlockCombination { TheoryBlock = b })
            .ToList();
    }

    /// <summary>
    /// Lab blokları üretir. Tek lab dersliği yeterliyse tek blok, yetmezse
    /// section'ı birden fazla lab grubuna böler.
    /// </summary>
    private List<BlockCombination> GenerateLabBlocks(
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
        // Try single lab room first (capacity >= section capacity)
        var singleRoomClassrooms = FilterClassrooms(allClassrooms, requiredClassroomType, section.Capacity, facultyClassroomIds);
        if (singleRoomClassrooms.Count > 0)
        {
            var blocks = FindBlocks(BlockType.Lab, requiredHours, singleRoomClassrooms, state, timeSlotsByDay, sortedDays, excludeBlocks);
            return blocks
                .Take(MaxCombinations)
                .Select(b => new BlockCombination { LabBlocks = new List<BlockAssignment> { b } })
                .ToList();
        }

        // No single room fits — split into multiple lab groups
        // Find the largest lab classroom to determine group count
        var allLabClassrooms = FilterClassrooms(allClassrooms, requiredClassroomType, 1, facultyClassroomIds);
        if (allLabClassrooms.Count == 0)
            return new List<BlockCombination>();

        var maxLabCapacity = allLabClassrooms.Max(c => c.Capacity);
        var groupCount = (int)Math.Ceiling((double)section.Capacity / maxLabCapacity);
        var minCapacityPerGroup = (int)Math.Ceiling((double)section.Capacity / groupCount);

        var labClassrooms = FilterClassrooms(allClassrooms, requiredClassroomType, minCapacityPerGroup, facultyClassroomIds);
        if (labClassrooms.Count == 0)
            return new List<BlockCombination>();

        var labCandidates = FindBlocks(BlockType.Lab, requiredHours, labClassrooms, state, timeSlotsByDay, sortedDays, excludeBlocks);
        if (labCandidates.Count < groupCount)
            return new List<BlockCombination>();

        // Generate combinations of `groupCount` non-overlapping lab blocks
        var results = new List<BlockCombination>();
        for (var i = 0; i < labCandidates.Count && results.Count < MaxCombinations; i++)
        {
            var first = labCandidates[i];
            if (groupCount == 2)
            {
                for (var j = i + 1; j < labCandidates.Count && results.Count < MaxCombinations; j++)
                {
                    var second = labCandidates[j];
                    if (!HasTimeOverlap(first, second))
                    {
                        results.Add(new BlockCombination
                        {
                            LabBlocks = new List<BlockAssignment> { first, second }
                        });
                    }
                }
            }
            else if (groupCount == 3)
            {
                for (var j = i + 1; j < labCandidates.Count && results.Count < MaxCombinations; j++)
                {
                    var second = labCandidates[j];
                    if (HasTimeOverlap(first, second)) continue;
                    for (var k = j + 1; k < labCandidates.Count && results.Count < MaxCombinations; k++)
                    {
                        var third = labCandidates[k];
                        if (!HasTimeOverlap(first, third) && !HasTimeOverlap(second, third))
                        {
                            results.Add(new BlockCombination
                            {
                                LabBlocks = new List<BlockAssignment> { first, second, third }
                            });
                        }
                    }
                }
            }
            else
            {
                // 4+ groups: just pair sequentially (unlikely case)
                var group = new List<BlockAssignment> { first };
                for (var j = i + 1; j < labCandidates.Count && group.Count < groupCount; j++)
                {
                    if (group.All(g => !HasTimeOverlap(g, labCandidates[j])))
                        group.Add(labCandidates[j]);
                }
                if (group.Count == groupCount)
                    results.Add(new BlockCombination { LabBlocks = group });
            }
        }

        return results;
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
        var theoryBlocks = FindBlocks(BlockType.Theory, section.TheoryHours, theoryClassrooms, state, timeSlotsByDay, sortedDays, excludeBlocks);

        // Generate lab combinations (single or multi-group)
        var labCombos = GenerateLabBlocks(
            section.LabHours, labType, section, state,
            allClassrooms, timeSlotsByDay, sortedDays, facultyClassroomIds, excludeBlocks);

        var totalHours = section.TheoryHours + section.LabHours;
        var mustSplitDays = totalHours > splitThreshold;

        var results = new List<BlockCombination>();

        foreach (var theory in theoryBlocks)
        {
            foreach (var labCombo in labCombos)
            {
                // Check theory vs all lab blocks for overlap
                var valid = true;
                foreach (var lab in labCombo.LabBlocks)
                {
                    if (mustSplitDays && theory.Day == lab.Day)
                    { valid = false; break; }

                    if (!mustSplitDays && theory.Day == lab.Day && HasTimeOverlap(theory, lab))
                    { valid = false; break; }
                }

                if (!valid) continue;

                results.Add(new BlockCombination
                {
                    TheoryBlock = theory,
                    LabBlocks = labCombo.LabBlocks
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
        // For general teaching types (Lecture, Seminar, Amphitheater), accept any general teaching classroom.
        // For lab types (Lab, Computer), accept any lab teaching classroom.
        // For other specific types, require exact match.
        var isGeneralType = GeneralTeachingTypes.Contains(requiredType);
        var isLabType = LabTeachingTypes.Contains(requiredType);

        return allClassrooms
            .Where(c => facultyClassroomIds.Contains(c.Id))
            .Where(c => isGeneralType
                ? GeneralTeachingTypes.Contains(c.Type)
                : isLabType
                    ? LabTeachingTypes.Contains(c.Type)
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

    /// <summary>
    /// Online dersler için candidate üretir. Derslik iterasyonu yok — ClassroomId=null olarak döner.
    /// Hafta sonu dahil tüm günleri kullanabilir. Teori+Lab saatlerini tek pencerede toplar (split threshold hariç).
    /// </summary>
    private static List<BlockCombination> GenerateOnlineCandidates(
        SectionSnapshot section,
        MutableScheduleState state,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        int splitThreshold,
        HashSet<BlockAssignment>? excludeBlocks)
    {
        var totalHours = section.TheoryHours + section.LabHours;
        if (totalHours == 0)
            return new List<BlockCombination>();

        // Online dersler tüm günleri kullanabilir (hafta sonu da dahil)
        var allDays = timeSlotsByDay.Keys
            .OrderBy(d => state.GetDaySectionCount(d))
            .ThenBy(d => d)
            .ToList();

        var results = new List<BlockCombination>();
        var mustSplit = totalHours > splitThreshold && section.TheoryHours > 0 && section.LabHours > 0;

        if (!mustSplit)
        {
            // Tek pencere: totalHours ardışık slot
            var blocks = FindOnlineBlocks(section.TheoryHours > 0 ? BlockType.Theory : BlockType.Lab,
                totalHours, state, timeSlotsByDay, allDays, excludeBlocks);

            foreach (var block in blocks.Take(MaxCombinations))
            {
                results.Add(new BlockCombination { TheoryBlock = block });
            }
            return results;
        }

        // Split: teori + lab ayrı günlerde
        var theoryBlocks = FindOnlineBlocks(BlockType.Theory, section.TheoryHours, state, timeSlotsByDay, allDays, excludeBlocks);
        var labBlocks = FindOnlineBlocks(BlockType.Lab, section.LabHours, state, timeSlotsByDay, allDays, excludeBlocks);

        foreach (var t in theoryBlocks)
        {
            foreach (var l in labBlocks)
            {
                if (t.Day == l.Day) continue;
                results.Add(new BlockCombination { TheoryBlock = t, LabBlocks = new List<BlockAssignment> { l } });
                if (results.Count >= MaxCombinations) return results;
            }
        }

        return results;
    }

    private static List<BlockAssignment> FindOnlineBlocks(
        BlockType blockType,
        int requiredHours,
        MutableScheduleState state,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        List<DayOfWeek> sortedDays,
        HashSet<BlockAssignment>? excludeBlocks)
    {
        var results = new List<BlockAssignment>();

        foreach (var day in sortedDays)
        {
            if (!timeSlotsByDay.TryGetValue(day, out var daySlots) || daySlots.Count == 0)
                continue;

            var sorted = daySlots.OrderBy(s => s.StartTime).ToList();
            var windowsFound = 0;

            for (var i = 0; i <= sorted.Count - requiredHours; i++)
            {
                if (windowsFound >= MaxWindowsPerDay)
                    break;

                // Ardışık slot kontrolü (FindBlocks'taki mantık)
                var valid = true;
                for (var j = 0; j < requiredHours - 1; j++)
                {
                    var current = sorted[i + j];
                    var next = sorted[i + j + 1];
                    var gap = next.StartTime - current.EndTime;
                    if (gap.TotalMinutes > 10 || gap.TotalMinutes < 0)
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid) continue;

                var windowStart = sorted[i].StartTime;
                var windowEnd = sorted[i + requiredHours - 1].EndTime;

                var block = new BlockAssignment(blockType, null, day, windowStart, windowEnd);

                if (excludeBlocks != null && excludeBlocks.Contains(block))
                    continue;

                results.Add(block);
                windowsFound++;
            }
        }

        return results;
    }
}
