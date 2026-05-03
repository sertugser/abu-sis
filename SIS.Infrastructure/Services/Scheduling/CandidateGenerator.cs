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
    // Faz 2: sabitler config-driven oldu, default'lar geriye uyumluluk için.
    // AutoScheduleEngine.RunAsync başında Configure() çağrılır.
    public int MaxCombinations { get; private set; } = 200;
    public int MaxWindowsPerDay { get; private set; } = 5;
    public int MaxClassroomsPerType { get; private set; } = 10;

    /// <summary>
    /// Config'ten okunan limitleri uygula. AutoScheduleEngine her çalıştırmada çağırır.
    /// </summary>
    public void Configure(int maxCombinations, int maxWindowsPerDay, int maxClassroomsPerType)
    {
        MaxCombinations = Math.Max(1, maxCombinations);
        MaxWindowsPerDay = Math.Max(1, maxWindowsPerDay);
        MaxClassroomsPerType = Math.Max(1, maxClassroomsPerType);
    }

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
        HashSet<BlockAssignment>? excludeBlocks = null,
        HashSet<BlockType>? splittableBlockTypes = null)
    {
        var hasTheory = section.TheoryHours > 0;
        var hasLab = section.LabHours > 0;

        // Varsayılan: sadece Lab split'i yapılabilir. Config'ten farklı gelirse üzerine yazar.
        var splittable = splittableBlockTypes ?? new HashSet<BlockType> { BlockType.Lab };

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
                splitThreshold, facultyClassroomIds, excludeBlocks, splittable);

        if (hasTheory)
            return GenerateSingleBlock(
                BlockType.Theory,
                section.TheoryHours,
                section.TheoryClassroomType ?? ClassroomType.Lecture,
                section, state, allClassrooms, timeSlotsByDay, sortedDays,
                facultyClassroomIds, excludeBlocks, splittable);

        if (hasLab)
            return GenerateSingleBlock(
                BlockType.Lab,
                section.LabHours,
                section.LabClassroomType ?? ClassroomType.Lab,
                section, state, allClassrooms, timeSlotsByDay, sortedDays,
                facultyClassroomIds, excludeBlocks, splittable);

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
        HashSet<BlockAssignment>? excludeBlocks,
        HashSet<BlockType> splittableBlockTypes)
    {
        // Split izinli block type için kapasite-sığmayan durumda paralel oda split'i dene
        if (splittableBlockTypes.Contains(blockType))
        {
            return GenerateSplittableBlocks(
                blockType, requiredHours, requiredClassroomType, section, state,
                allClassrooms, timeSlotsByDay, sortedDays, facultyClassroomIds, excludeBlocks);
        }

        var classrooms = FilterClassrooms(allClassrooms, requiredClassroomType, section.Capacity, facultyClassroomIds, state);
        var blocks = FindBlocks(blockType, requiredHours, classrooms, state, timeSlotsByDay, sortedDays, excludeBlocks);

        return blocks
            .Take(MaxCombinations)
            .Select(b => blockType == BlockType.Lab
                ? new BlockCombination { LabBlocks = new List<BlockAssignment> { b } }
                : new BlockCombination { TheoryBlock = b })
            .ToList();
    }

    /// <summary>
    /// Kapasite yetersiz ise paralel oda split'i yapar (aynı zaman dilimi, farklı odalar).
    /// Proliz pattern'ı: BM 101 Java I Lab, 80 kişi, Salı 14:00 → L139 + L141 aynı anda.
    /// Tek oda yeterliyse tek block döner. Yetmezse aynı slot'ta birden fazla oda'ya böler.
    /// </summary>
    private List<BlockCombination> GenerateSplittableBlocks(
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
        // 1) Tek oda yeterli mi? Try single room first (capacity >= section capacity)
        var singleRoomClassrooms = FilterClassrooms(allClassrooms, requiredClassroomType, section.Capacity, facultyClassroomIds, state);
        if (singleRoomClassrooms.Count > 0)
        {
            var blocks = FindBlocks(blockType, requiredHours, singleRoomClassrooms, state, timeSlotsByDay, sortedDays, excludeBlocks);
            return blocks
                .Take(MaxCombinations)
                .Select(b => blockType == BlockType.Lab
                    ? new BlockCombination { LabBlocks = new List<BlockAssignment> { b } }
                    : new BlockCombination { TheoryBlock = b })
                .ToList();
        }

        // 2) Tek oda yetmiyor — paralel split dene
        var allMatchingClassrooms = FilterClassrooms(allClassrooms, requiredClassroomType, 1, facultyClassroomIds, state);
        if (allMatchingClassrooms.Count == 0)
            return new List<BlockCombination>();

        var maxCapacity = allMatchingClassrooms.Max(c => c.Capacity);
        var groupCount = (int)Math.Ceiling((double)section.Capacity / maxCapacity);
        var minCapacityPerGroup = (int)Math.Ceiling((double)section.Capacity / groupCount);

        var splitClassrooms = FilterClassrooms(allClassrooms, requiredClassroomType, minCapacityPerGroup, facultyClassroomIds, state);
        if (splitClassrooms.Count < groupCount)
            return new List<BlockCombination>();

        var allSplitCandidates = FindBlocks(blockType, requiredHours, splitClassrooms, state, timeSlotsByDay, sortedDays, excludeBlocks);
        if (allSplitCandidates.Count < groupCount)
            return new List<BlockCombination>();

        // 3) AYNI zaman diliminde (Day + StartTime + EndTime) groupCount adet farklı oda bulunan
        // pencereler arasından kombinasyon üret. Paralel split = aynı saat, farklı oda.
        var results = new List<BlockCombination>();
        var windowGroups = allSplitCandidates
            .GroupBy(b => (b.Day, b.StartTime, b.EndTime))
            .Where(g => g.Count() >= groupCount)
            .ToList();

        foreach (var windowGroup in windowGroups)
        {
            var roomsInWindow = windowGroup
                .OrderBy(b => ClassroomCapacityOf(b.ClassroomId, allMatchingClassrooms))
                .ToList();

            // Sliding window: [0..groupCount), [1..groupCount+1), ... farklı oda kombinasyonları
            for (var i = 0; i <= roomsInWindow.Count - groupCount; i++)
            {
                if (results.Count >= MaxCombinations)
                    return results;

                var combo = roomsInWindow.Skip(i).Take(groupCount).ToList();
                var totalCapacity = combo.Sum(b => ClassroomCapacityOf(b.ClassroomId, allMatchingClassrooms));
                if (totalCapacity < section.Capacity)
                    continue; // toplam kapasite hâlâ yetmiyorsa atla

                var blockCombo = blockType == BlockType.Lab
                    ? new BlockCombination { LabBlocks = combo }
                    : new BlockCombination { TheoryBlock = combo[0], LabBlocks = combo.Skip(1).ToList() };

                results.Add(blockCombo);
            }
        }

        return results;
    }

    private static int ClassroomCapacityOf(int? classroomId, List<Classroom> classrooms)
    {
        if (!classroomId.HasValue) return 0;
        var cr = classrooms.FirstOrDefault(c => c.Id == classroomId.Value);
        return cr?.Capacity ?? 0;
    }

    private List<BlockCombination> GenerateMixed(
        SectionSnapshot section,
        MutableScheduleState state,
        List<Classroom> allClassrooms,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        List<DayOfWeek> sortedDays,
        int splitThreshold,
        HashSet<int> facultyClassroomIds,
        HashSet<BlockAssignment>? excludeBlocks,
        HashSet<BlockType> splittableBlockTypes)
    {
        var theoryType = section.TheoryClassroomType ?? ClassroomType.Lecture;
        var labType = section.LabClassroomType ?? ClassroomType.Lab;

        // Teori bloğu: split izinli mi diye bak; çoğu zaman değildir, tek oda bulur.
        List<BlockAssignment> theoryBlocks;
        if (splittableBlockTypes.Contains(BlockType.Theory))
        {
            var theorySplitCombos = GenerateSplittableBlocks(
                BlockType.Theory, section.TheoryHours, theoryType, section, state,
                allClassrooms, timeSlotsByDay, sortedDays, facultyClassroomIds, excludeBlocks);
            // Mixed mode'da teori tek oda varsayılıyor; eğer theory split olursa burada
            // TheoryBlock ilk oda, LabBlocks eklenmiş olur — bu durumda mixed kombinasyon
            // karmaşıklaşır. Pratikte teori split nadir, şu an sadece ilk TheoryBlock'u alıyoruz.
            theoryBlocks = theorySplitCombos
                .Where(c => c.TheoryBlock != null)
                .Select(c => c.TheoryBlock!)
                .ToList();
        }
        else
        {
            var theoryClassrooms = FilterClassrooms(allClassrooms, theoryType, section.Capacity, facultyClassroomIds, state);
            theoryBlocks = FindBlocks(BlockType.Theory, section.TheoryHours, theoryClassrooms, state, timeSlotsByDay, sortedDays, excludeBlocks);
        }

        // Lab kombinasyonları: split izinli ise paralel split dene; değilse tek oda zorunlu.
        List<BlockCombination> labCombos;
        if (splittableBlockTypes.Contains(BlockType.Lab))
        {
            labCombos = GenerateSplittableBlocks(
                BlockType.Lab, section.LabHours, labType, section, state,
                allClassrooms, timeSlotsByDay, sortedDays, facultyClassroomIds, excludeBlocks);
        }
        else
        {
            var labClassrooms = FilterClassrooms(allClassrooms, labType, section.Capacity, facultyClassroomIds, state);
            var labBlocks = FindBlocks(BlockType.Lab, section.LabHours, labClassrooms, state, timeSlotsByDay, sortedDays, excludeBlocks);
            labCombos = labBlocks
                .Take(MaxCombinations)
                .Select(b => new BlockCombination { LabBlocks = new List<BlockAssignment> { b } })
                .ToList();
        }

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

    private List<Classroom> FilterClassrooms(
        List<Classroom> allClassrooms,
        ClassroomType requiredType,
        int requiredCapacity,
        HashSet<int> facultyClassroomIds,
        MutableScheduleState state)
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
            // Faz 1.2 (revize): kapasite waste'ini önlemek için önce kapasiteye yakın olanı seç,
            // EŞİT kapasitede tiebreaker olarak az kullanılan dersliği öne çek (load balance).
            // İlk versiyon (load-first) regresyona yol açıyordu: küçük section büyük amfiyi
            // dolduruyordu, büyük section yer bulamıyordu.
            .OrderBy(c => c.Type == requiredType ? 0 : 1) // prefer exact match first
            .ThenBy(c => c.Capacity)                       // smallest sufficient (no waste)
            .ThenBy(c => state.GetClassroomSlotCount(c.Id)) // tiebreaker: load balance
            .Take(MaxClassroomsPerType)
            .ToList();
    }

    private List<BlockAssignment> FindBlocks(
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
    private List<BlockCombination> GenerateOnlineCandidates(
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

    private List<BlockAssignment> FindOnlineBlocks(
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
