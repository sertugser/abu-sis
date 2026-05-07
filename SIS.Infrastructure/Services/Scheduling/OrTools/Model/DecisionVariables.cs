using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Model;

/// <summary>
/// CP-SAT decision variable registry. Section başına lecture/lab interval'ları,
/// component başına room IntVar'ları tutar. Encoder'lar buradan değişken alır.
/// </summary>
public sealed class DecisionVariables
{
    private readonly CpModel _model;
    private readonly int _totalSlots;

    private readonly Dictionary<int, IntervalVar> _lectureIntervals = new();
    private readonly Dictionary<int, IntervalVar> _labIntervals = new();
    private readonly Dictionary<int, IntVar> _lectureRooms = new();
    private readonly Dictionary<int, IntVar> _labRooms = new();

    public DecisionVariables(CpModel model, int totalSlots)
    {
        if (totalSlots <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(totalSlots), totalSlots,
                "totalSlots must be positive.");

        _model = model;
        _totalSlots = totalSlots;
    }

    public void CreateIntervalsForSection(SectionInput section)
    {
        if (section.TheoryHours > 0)
        {
            var start = _model.NewIntVar(0, _totalSlots - section.TheoryHours,
                $"theory_start_s{section.Id}");
            var end = _model.NewIntVar(section.TheoryHours, _totalSlots,
                $"theory_end_s{section.Id}");
            var interval = _model.NewIntervalVar(start, section.TheoryHours, end,
                $"theory_iv_s{section.Id}");
            _lectureIntervals[section.Id] = interval;
        }

        if (section.LabHours > 0)
        {
            var start = _model.NewIntVar(0, _totalSlots - section.LabHours,
                $"lab_start_s{section.Id}");
            var end = _model.NewIntVar(section.LabHours, _totalSlots,
                $"lab_end_s{section.Id}");
            var interval = _model.NewIntervalVar(start, section.LabHours, end,
                $"lab_iv_s{section.Id}");
            _labIntervals[section.Id] = interval;
        }
    }

    public void CreateRoomVariable(int sectionId, ComponentType componentType,
        IReadOnlyList<ClassroomInput> classrooms)
    {
        var allowedIds = classrooms.Select(c => (long)c.Id).ToArray();
        var room = _model.NewIntVarFromDomain(
            Google.OrTools.Util.Domain.FromValues(allowedIds),
            $"room_{componentType.ToString().ToLowerInvariant()}_s{sectionId}");

        switch (componentType)
        {
            case ComponentType.Lecture:
                _lectureRooms[sectionId] = room;
                break;
            case ComponentType.Lab:
                _labRooms[sectionId] = room;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(componentType), componentType,
                    $"Unsupported component type: {componentType}");
        }
    }

    /// <summary>
    /// Section'ın lecture interval'ını döner.
    /// Section hiç register edilmemişse KeyNotFoundException.
    /// Section register edilmiş ama lecture component yoksa (sadece lab) null.
    /// </summary>
    public IntervalVar? GetLectureInterval(int sectionId)
    {
        if (_lectureIntervals.TryGetValue(sectionId, out var iv)) return iv;
        if (_labIntervals.ContainsKey(sectionId)) return null;
        throw new KeyNotFoundException($"Section {sectionId} not registered.");
    }

    /// <summary>
    /// Section'ın lab interval'ını döner.
    /// Section hiç register edilmemişse KeyNotFoundException.
    /// Section register edilmiş ama lab component yoksa (sadece teori) null.
    /// </summary>
    public IntervalVar? GetLabInterval(int sectionId)
    {
        if (_labIntervals.TryGetValue(sectionId, out var iv)) return iv;
        if (_lectureIntervals.ContainsKey(sectionId)) return null;
        throw new KeyNotFoundException($"Section {sectionId} not registered.");
    }

    public IntVar GetLectureRoom(int sectionId) =>
        _lectureRooms.TryGetValue(sectionId, out var v) ? v
        : throw new KeyNotFoundException($"No lecture room variable for section {sectionId}.");

    public IntVar GetLabRoom(int sectionId) =>
        _labRooms.TryGetValue(sectionId, out var v) ? v
        : throw new KeyNotFoundException($"No lab room variable for section {sectionId}.");

    public IReadOnlyDictionary<int, IntervalVar> AllLectureIntervals => _lectureIntervals;
    public IReadOnlyDictionary<int, IntervalVar> AllLabIntervals => _labIntervals;

    private readonly Dictionary<(int sectionId, int classroomId, ComponentType type), IntervalVar> _optionalIntervals = new();
    private readonly Dictionary<(int sectionId, int classroomId, ComponentType type), ILiteral> _presenceLiterals = new();

    /// <summary>
    /// Section'ın belirli derslikte belirli component için optional interval'ını oluşturur.
    /// Presence literal: room[sectionId] == classroomId.
    /// Used by classroom-aware encoders (H1, H3, etc.) for per-classroom AddNoOverlap grouping.
    /// Idempotent: returns existing interval if already created for the same (sectionId, classroomId, componentType).
    /// </summary>
    public IntervalVar CreateOptionalRoomInterval(
        int sectionId,
        int classroomId,
        ComponentType componentType,
        int duration)
    {
        var key = (sectionId, classroomId, componentType);
        if (_optionalIntervals.TryGetValue(key, out var existing))
            return existing;

        // Create presence literal: room[sectionId] == classroomId ↔ presence
        var presence = _model.NewBoolVar(
            $"present_{componentType.ToString().ToLowerInvariant()}_s{sectionId}_c{classroomId}");

        var roomVar = componentType == ComponentType.Lecture
            ? GetLectureRoom(sectionId)
            : GetLabRoom(sectionId);
        _model.Add(roomVar == classroomId).OnlyEnforceIf(presence);
        _model.Add(roomVar != classroomId).OnlyEnforceIf(presence.Not());

        // Use existing component interval's start/end as the optional interval's bounds
        var sourceInterval = componentType == ComponentType.Lecture
            ? GetLectureInterval(sectionId)
            : GetLabInterval(sectionId);
        if (sourceInterval == null)
            throw new InvalidOperationException(
                $"Section {sectionId} has no {componentType} interval; cannot create optional room interval.");

        // Create optional interval gated by presence; same start/duration/end as source interval
        var optInterval = _model.NewOptionalIntervalVar(
            sourceInterval.StartExpr(),
            LinearExpr.Constant(duration),
            sourceInterval.EndExpr(),
            presence,
            $"opt_iv_{componentType.ToString().ToLowerInvariant()}_s{sectionId}_c{classroomId}");

        _optionalIntervals[key] = optInterval;
        _presenceLiterals[key] = presence;
        return optInterval;
    }

    public IReadOnlyDictionary<(int, int, ComponentType), IntervalVar> AllOptionalIntervals => _optionalIntervals;
    public IReadOnlyDictionary<(int, int, ComponentType), ILiteral> AllPresenceLiterals => _presenceLiterals;
}
