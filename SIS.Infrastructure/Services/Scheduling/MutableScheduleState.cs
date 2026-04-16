using SIS.Domain.Entities;

namespace SIS.Infrastructure.Services.Scheduling;

public enum BlockType { Theory, Lab }

public sealed record BlockAssignment(
    BlockType Type,
    int ClassroomId,
    DayOfWeek Day,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed class SectionPlacement
{
    public SectionSnapshot Snapshot { get; init; } = null!;
    public List<BlockAssignment> Blocks { get; init; } = new();
    public bool IsPinned { get; init; }
    public int AssignmentOrder { get; init; }
}

public sealed class MutableScheduleState
{
    public int TermId { get; }

    // Primary storage
    private readonly Dictionary<int, SectionPlacement> _placements = new();
    private int _nextOrder;

    // Occupancy indexes: (classroomId, day) -> list of (start, end, sectionId)
    private readonly Dictionary<(int ClassroomId, DayOfWeek Day), List<(TimeOnly Start, TimeOnly End, int SectionId)>> _classroomOccupancy = new();

    // Instructor indexes: (instructorId, day) -> list of (start, end, sectionId)
    private readonly Dictionary<(int InstructorId, DayOfWeek Day), List<(TimeOnly Start, TimeOnly End, int SectionId)>> _instructorOccupancy = new();

    // Day-level section counts
    private readonly Dictionary<DayOfWeek, int> _daySectionCounts = new();

    // Classroom lookup for enriching snapshots after SA moves
    private IReadOnlyDictionary<int, Classroom>? _classroomLookup;

    public MutableScheduleState(int termId)
    {
        TermId = termId;
    }

    // Private constructor for Clone
    private MutableScheduleState(int termId, int nextOrder)
    {
        TermId = termId;
        _nextOrder = nextOrder;
    }

    public void SetClassroomLookup(IReadOnlyDictionary<int, Classroom> lookup)
    {
        _classroomLookup = lookup;
    }

    public void Assign(int sectionId, SectionSnapshot snapshot, List<BlockAssignment> blocks, bool isPinned = false)
    {
        if (_placements.ContainsKey(sectionId))
            throw new InvalidOperationException($"Section {sectionId} is already assigned.");

        var placement = new SectionPlacement
        {
            Snapshot = snapshot,
            Blocks = blocks,
            IsPinned = isPinned,
            AssignmentOrder = _nextOrder++
        };

        _placements[sectionId] = placement;

        // Track which days this section spans (for day balance counting)
        var days = new HashSet<DayOfWeek>();

        foreach (var block in blocks)
        {
            // Classroom occupancy
            var crKey = (block.ClassroomId, block.Day);
            if (!_classroomOccupancy.TryGetValue(crKey, out var crSlots))
            {
                crSlots = new List<(TimeOnly, TimeOnly, int)>();
                _classroomOccupancy[crKey] = crSlots;
            }
            crSlots.Add((block.StartTime, block.EndTime, sectionId));

            // Instructor occupancy
            if (snapshot.InstructorId.HasValue)
            {
                var instrKey = (snapshot.InstructorId.Value, block.Day);
                if (!_instructorOccupancy.TryGetValue(instrKey, out var instrSlots))
                {
                    instrSlots = new List<(TimeOnly, TimeOnly, int)>();
                    _instructorOccupancy[instrKey] = instrSlots;
                }
                instrSlots.Add((block.StartTime, block.EndTime, sectionId));
            }

            days.Add(block.Day);
        }

        // Increment day counts (one per unique day the section touches)
        foreach (var day in days)
        {
            _daySectionCounts.TryGetValue(day, out var count);
            _daySectionCounts[day] = count + 1;
        }
    }

    public void Unassign(int sectionId)
    {
        if (!_placements.TryGetValue(sectionId, out var placement))
            return;

        var days = new HashSet<DayOfWeek>();

        foreach (var block in placement.Blocks)
        {
            // Remove from classroom occupancy
            var crKey = (block.ClassroomId, block.Day);
            if (_classroomOccupancy.TryGetValue(crKey, out var crSlots))
            {
                crSlots.RemoveAll(s => s.SectionId == sectionId);
                if (crSlots.Count == 0)
                    _classroomOccupancy.Remove(crKey);
            }

            // Remove from instructor occupancy
            if (placement.Snapshot.InstructorId.HasValue)
            {
                var instrKey = (placement.Snapshot.InstructorId.Value, block.Day);
                if (_instructorOccupancy.TryGetValue(instrKey, out var instrSlots))
                {
                    instrSlots.RemoveAll(s => s.SectionId == sectionId);
                    if (instrSlots.Count == 0)
                        _instructorOccupancy.Remove(instrKey);
                }
            }

            days.Add(block.Day);
        }

        // Decrement day counts
        foreach (var day in days)
        {
            if (_daySectionCounts.TryGetValue(day, out var count))
            {
                var newCount = count - 1;
                if (newCount <= 0)
                    _daySectionCounts.Remove(day);
                else
                    _daySectionCounts[day] = newCount;
            }
        }

        _placements.Remove(sectionId);
    }

    public bool IsAssigned(int sectionId) => _placements.ContainsKey(sectionId);

    public bool IsPinned(int sectionId) =>
        _placements.TryGetValue(sectionId, out var p) && p.IsPinned;

    public SectionPlacement? GetPlacement(int sectionId) =>
        _placements.GetValueOrDefault(sectionId);

    public IReadOnlyDictionary<int, SectionPlacement> AllPlacements => _placements;

    public List<(DayOfWeek Day, TimeOnly Start, TimeOnly End, int SectionId)> GetClassroomOccupancy(int classroomId, DayOfWeek day)
    {
        if (_classroomOccupancy.TryGetValue((classroomId, day), out var slots))
            return slots.Select(s => (day, s.Start, s.End, s.SectionId)).ToList();
        return new();
    }

    public List<(DayOfWeek Day, TimeOnly Start, TimeOnly End, int SectionId)> GetInstructorOccupancy(int instructorId, DayOfWeek day)
    {
        if (_instructorOccupancy.TryGetValue((instructorId, day), out var slots))
            return slots.Select(s => (day, s.Start, s.End, s.SectionId)).ToList();
        return new();
    }

    public int GetDaySectionCount(DayOfWeek day) =>
        _daySectionCounts.GetValueOrDefault(day);

    public DayOfWeek GetLeastLoadedDay(IReadOnlyList<DayOfWeek> candidates)
    {
        if (candidates.Count == 0)
            throw new ArgumentException("Candidates list cannot be empty.", nameof(candidates));

        var best = candidates[0];
        var bestCount = GetDaySectionCount(best);

        for (var i = 1; i < candidates.Count; i++)
        {
            var count = GetDaySectionCount(candidates[i]);
            if (count < bestCount)
            {
                best = candidates[i];
                bestCount = count;
            }
        }

        return best;
    }

    public List<int> GetAssignedNonPinnedSectionIds() =>
        _placements
            .Where(kvp => !kvp.Value.IsPinned)
            .Select(kvp => kvp.Key)
            .ToList();

    public SectionSnapshot ToSectionSnapshot(int sectionId)
    {
        var placement = _placements[sectionId];
        var snap = placement.Snapshot;

        var timeBlocks = placement.Blocks.Select(b => new TimeBlock
        {
            Day = b.Day,
            StartTime = b.StartTime,
            EndTime = b.EndTime,
            ClassroomId = b.ClassroomId
        }).ToList();

        var totalHours = placement.Blocks.Sum(b =>
            (int)Math.Ceiling((b.EndTime - b.StartTime).TotalHours));

        // Resolve actual classroom data from blocks (fixes stale data after SA swap/chain)
        var actualClassroomId = placement.Blocks.Count > 0 ? placement.Blocks[0].ClassroomId : snap.ClassroomId;
        var actualClassroomName = snap.ClassroomName;
        var actualClassroomCapacity = snap.ClassroomCapacity;
        var actualClassroomType = snap.ClassroomType;

        if (actualClassroomId.HasValue && _classroomLookup != null
            && _classroomLookup.TryGetValue(actualClassroomId.Value, out var actualClassroom))
        {
            actualClassroomName = actualClassroom.Name;
            actualClassroomCapacity = actualClassroom.Capacity;
            actualClassroomType = actualClassroom.Type;
        }

        return new SectionSnapshot
        {
            SectionId = snap.SectionId,
            SectionNumber = snap.SectionNumber,
            CourseId = snap.CourseId,
            CourseCode = snap.CourseCode,
            CourseName = snap.CourseName,
            TermId = snap.TermId,
            TheoryHours = snap.TheoryHours,
            LabHours = snap.LabHours,
            Capacity = snap.Capacity,
            EnrolledCount = snap.EnrolledCount,
            ClassroomId = actualClassroomId,
            ClassroomName = actualClassroomName,
            ClassroomCapacity = actualClassroomCapacity,
            ClassroomType = actualClassroomType,
            TheoryClassroomType = snap.TheoryClassroomType,
            LabClassroomType = snap.LabClassroomType,
            InstructorId = snap.InstructorId,
            InstructorName = snap.InstructorName,
            InstructorMaxWeeklyHours = snap.InstructorMaxWeeklyHours,
            DepartmentId = snap.DepartmentId,
            SuggestedSemester = snap.SuggestedSemester,
            SuggestedSemestersByDepartment = snap.SuggestedSemestersByDepartment,
            AccessibleDepartmentIds = snap.AccessibleDepartmentIds,
            AssignedTimeSlotCount = totalHours,
            TimeBlocks = timeBlocks,
            Notes = snap.Notes
        };
    }

    public TermSnapshot ToTermSnapshot(TermSnapshot baseSnapshot)
    {
        var sections = _placements.Keys
            .Select(ToSectionSnapshot)
            .ToList();

        return new TermSnapshot
        {
            TermId = TermId,
            TermName = baseSnapshot.TermName,
            ScopeName = baseSnapshot.ScopeName,
            Sections = sections,
            ClassroomAssignments = baseSnapshot.ClassroomAssignments,
            DeptFacultyMap = baseSnapshot.DeptFacultyMap,
            Corequisites = baseSnapshot.Corequisites,
            CoEnrollmentPairs = baseSnapshot.CoEnrollmentPairs,
            CourseSuggestedSemestersByDepartment = baseSnapshot.CourseSuggestedSemestersByDepartment,
            CourseAccessibleDepartments = baseSnapshot.CourseAccessibleDepartments,
            InstructorPreferences = baseSnapshot.InstructorPreferences,
        };
    }

    public MutableScheduleState Clone()
    {
        var clone = new MutableScheduleState(TermId, _nextOrder);
        clone._classroomLookup = _classroomLookup;

        // Deep copy placements
        foreach (var (sectionId, placement) in _placements)
        {
            clone._placements[sectionId] = new SectionPlacement
            {
                Snapshot = placement.Snapshot, // SectionSnapshot is init-only, safe to share
                Blocks = placement.Blocks.ToList(), // BlockAssignment is a record (immutable), shallow copy of list suffices
                IsPinned = placement.IsPinned,
                AssignmentOrder = placement.AssignmentOrder
            };
        }

        // Deep copy classroom occupancy
        foreach (var (key, slots) in _classroomOccupancy)
            clone._classroomOccupancy[key] = slots.ToList();

        // Deep copy instructor occupancy
        foreach (var (key, slots) in _instructorOccupancy)
            clone._instructorOccupancy[key] = slots.ToList();

        // Deep copy day counts
        foreach (var (day, count) in _daySectionCounts)
            clone._daySectionCounts[day] = count;

        return clone;
    }
}
