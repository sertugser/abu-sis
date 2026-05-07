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
    // Plan 4 Task 2 — Parallel lab support: N IntVar per section (groupCount-based).
    // Single-room sections: list with one element. Single-room callers (GetLabRoom) read [0].
    private readonly Dictionary<int, IReadOnlyList<IntVar>> _labRooms = new();
    private readonly Dictionary<int, ILiteral> _sectionPresent = new();

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
        // Section-level presence boolean (Plan 3 Task 3 — partial scheduling).
        // present == 1 → solver places this section. present == 0 → section unscheduled.
        var present = _model.NewBoolVar($"present_s{section.Id}");
        _sectionPresent[section.Id] = present;

        if (section.TheoryHours > 0)
        {
            var start = _model.NewIntVar(0, _totalSlots - section.TheoryHours,
                $"theory_start_s{section.Id}");
            var end = _model.NewIntVar(section.TheoryHours, _totalSlots,
                $"theory_end_s{section.Id}");
            // Optional interval gated by section presence literal
            var interval = _model.NewOptionalIntervalVar(
                start,
                LinearExpr.Constant(section.TheoryHours),
                end,
                present,
                $"theory_iv_s{section.Id}");
            _lectureIntervals[section.Id] = interval;
        }

        if (section.LabHours > 0)
        {
            var start = _model.NewIntVar(0, _totalSlots - section.LabHours,
                $"lab_start_s{section.Id}");
            var end = _model.NewIntVar(section.LabHours, _totalSlots,
                $"lab_end_s{section.Id}");
            // Optional interval gated by section presence literal
            var interval = _model.NewOptionalIntervalVar(
                start,
                LinearExpr.Constant(section.LabHours),
                end,
                present,
                $"lab_iv_s{section.Id}");
            _labIntervals[section.Id] = interval;
        }
    }

    public void CreateRoomVariable(int sectionId, ComponentType componentType,
        IReadOnlyList<ClassroomInput> classrooms, int groupCount = 1)
    {
        var allowedIds = classrooms.Select(c => (long)c.Id).ToArray();

        switch (componentType)
        {
            case ComponentType.Lecture:
            {
                var room = _model.NewIntVarFromDomain(
                    Google.OrTools.Util.Domain.FromValues(allowedIds),
                    $"room_lecture_s{sectionId}");
                _lectureRooms[sectionId] = room;
                break;
            }
            case ComponentType.Lab:
            {
                if (groupCount < 1)
                    throw new ArgumentOutOfRangeException(
                        nameof(groupCount), groupCount,
                        "groupCount must be >= 1.");

                var labRooms = new List<IntVar>(groupCount);
                for (int i = 0; i < groupCount; i++)
                {
                    var room = _model.NewIntVarFromDomain(
                        Google.OrTools.Util.Domain.FromValues(allowedIds),
                        $"room_lab_s{sectionId}_g{i}");
                    labRooms.Add(room);
                }
                // Paralel split: tüm group'lar farklı odada (groupCount > 1 ise).
                if (groupCount > 1)
                    _model.AddAllDifferent(labRooms);
                _labRooms[sectionId] = labRooms;
                break;
            }
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

    /// <summary>
    /// Section'ın lab room IntVar'larından İLK group'unkini döner. Backward-compat shim:
    /// Plan 4 öncesi tek IntVar dünyasından kalan call site'lar için.
    /// Multi-group section'larda (groupCount > 1) tüm room'lara erişmek için <see cref="GetLabRooms"/> kullan.
    /// </summary>
    public IntVar GetLabRoom(int sectionId) =>
        _labRooms.TryGetValue(sectionId, out var rooms) && rooms.Count > 0
            ? rooms[0]
            : throw new KeyNotFoundException($"No lab room variable for section {sectionId}.");

    /// <summary>
    /// Section'ın tüm lab room IntVar'larını döner (paralel split desteği).
    /// Plan 4 Task 2: groupCount kadar IntVar; AddAllDifferent ile farklı oda zorunlu (groupCount > 1).
    /// </summary>
    public IReadOnlyList<IntVar> GetLabRooms(int sectionId) =>
        _labRooms.TryGetValue(sectionId, out var rooms)
            ? rooms
            : throw new KeyNotFoundException($"No lab rooms for section {sectionId}.");

    public IReadOnlyDictionary<int, IntervalVar> AllLectureIntervals => _lectureIntervals;
    public IReadOnlyDictionary<int, IntervalVar> AllLabIntervals => _labIntervals;
    public IReadOnlyDictionary<int, IReadOnlyList<IntVar>> AllLabRooms => _labRooms;

    /// <summary>
    /// Section'ın "yerleşti mi?" boolean literal'ı. Plan 3 Task 3 partial scheduling için eklendi.
    /// CP-SAT objective'i Maximize(sum(present)) → solver olabildiği kadar çok section yerleştirir.
    /// </summary>
    public ILiteral GetSectionPresent(int sectionId) =>
        _sectionPresent.TryGetValue(sectionId, out var p)
            ? p
            : throw new KeyNotFoundException($"Section {sectionId} has no presence literal.");

    public IReadOnlyDictionary<int, ILiteral> AllSectionPresent => _sectionPresent;

    private readonly Dictionary<(int sectionId, int classroomId, ComponentType type), IntervalVar> _optionalIntervals = new();
    private readonly Dictionary<(int sectionId, int classroomId, ComponentType type), ILiteral> _presenceLiterals = new();

    /// <summary>
    /// Section'ın belirli derslikte belirli component için optional interval'ını oluşturur.
    /// Presence literal: sectionPresent ∧ (room[sectionId] == classroomId).
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

        // Plan 3 Task 4.5: presence must be gated by both section-level present AND room equality.
        // Otherwise dropped sections still occupy slots via room IntVar's residual value.
        if (!_sectionPresent.TryGetValue(sectionId, out var sectionPresent))
            throw new InvalidOperationException(
                $"Section {sectionId} has no presence literal. Call CreateIntervalsForSection first.");

        // Step 1: Reify room equality (roomEqLit ⇔ (roomVar == classroomId))
        var roomVar = componentType == ComponentType.Lecture
            ? GetLectureRoom(sectionId)
            : GetLabRoom(sectionId);
        var roomEqLit = _model.NewBoolVar(
            $"room_eq_{componentType.ToString().ToLowerInvariant()}_s{sectionId}_c{classroomId}");
        _model.Add(roomVar == classroomId).OnlyEnforceIf(roomEqLit);
        _model.Add(roomVar != classroomId).OnlyEnforceIf(roomEqLit.Not());

        // Step 2: Combined presence — must be present AT section level AND in this specific classroom
        // presence ⇔ (sectionPresent ∧ roomEqLit)
        var presence = _model.NewBoolVar(
            $"present_{componentType.ToString().ToLowerInvariant()}_s{sectionId}_c{classroomId}");

        // presence → sectionPresent ∧ roomEqLit
        _model.AddBoolAnd(new ILiteral[] { sectionPresent, roomEqLit }).OnlyEnforceIf(presence);
        // ¬presence → ¬sectionPresent ∨ ¬roomEqLit
        _model.AddBoolOr(new ILiteral[] { sectionPresent.Not(), roomEqLit.Not() }).OnlyEnforceIf(presence.Not());

        // Use existing component interval's start/end as the optional interval's bounds
        var sourceInterval = componentType == ComponentType.Lecture
            ? GetLectureInterval(sectionId)
            : GetLabInterval(sectionId);
        if (sourceInterval == null)
            throw new InvalidOperationException(
                $"Section {sectionId} has no {componentType} interval; cannot create optional room interval.");

        // Optional interval gated by combined presence
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
