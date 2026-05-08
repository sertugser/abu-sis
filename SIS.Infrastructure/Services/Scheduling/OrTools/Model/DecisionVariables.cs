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
    // Plan 7b Task 7 — Multi-interval lab support: N lab intervals per section
    // (when LabHours > MaxLabBlockHours and CreateLabIntervalsForSection is called).
    // Single-interval call sites still use _labIntervals; GetLabIntervals falls back
    // to a single-element list for backward compat.
    private readonly Dictionary<int, IReadOnlyList<IntervalVar>> _labIntervalsList = new();
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

    public void CreateIntervalsForSection(
        SectionInput section,
        IReadOnlyList<TimeSlotInput>? timeSlots = null,
        bool createLabInterval = true)
    {
        // Section-level presence boolean (Plan 3 Task 3 — partial scheduling).
        // present == 1 → solver places this section. present == 0 → section unscheduled.
        var present = _model.NewBoolVar($"present_s{section.Id}");
        _sectionPresent[section.Id] = present;

        if (section.TheoryHours > 0)
        {
            var start = CreateStartIntVar(
                duration: section.TheoryHours,
                timeSlots: timeSlots,
                varName: $"theory_start_s{section.Id}",
                presence: present);
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

        // Plan 7b Task 7 — Skip single-interval lab when caller intends to use
        // CreateLabIntervalsForSection (multi-interval split). Default true
        // preserves backward compatibility with all existing call sites.
        if (section.LabHours > 0 && createLabInterval)
        {
            var start = CreateStartIntVar(
                duration: section.LabHours,
                timeSlots: timeSlots,
                varName: $"lab_start_s{section.Id}",
                presence: present);
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

    /// <summary>
    /// Plan 7b Task 7 — Multi-interval lab support. LabHours > maxLabBlockHours olan
    /// section'lar için <see cref="LabBlockSplitter.SplitLabIntoBlocks"/> ile N adet
    /// ayrı interval üretir. Her interval kendi start IntVar'ı, timeSlots ile slot-validated;
    /// her interval için ayrı day var oluşturulur ve day vars'a AddAllDifferent uygulanır
    /// (v1 BuildMultiLabCombinations paritesi — lab block'lar farklı günlere düşer).
    ///
    /// Çağıran (ScheduleModelBuilder) önce <see cref="CreateIntervalsForSection"/> ile
    /// presence literal + (varsa) theory interval'ı oluşturmalı; lab interval'ı bu
    /// method üretecekse <c>createLabInterval: false</c> geçilmelidir.
    /// LabHours ≤ maxLabBlockHours ise tek-elemanlı list döner; mevcut <c>_labIntervals</c>
    /// dict de güncellenir (backward compat: GetLabInterval / single-interval call sites çalışır).
    /// </summary>
    public void CreateLabIntervalsForSection(
        SectionInput section,
        IReadOnlyList<TimeSlotInput> timeSlots,
        int maxLabBlockHours)
    {
        if (section.LabHours <= 0)
            throw new InvalidOperationException(
                $"Section {section.Id} has no lab component; CreateLabIntervalsForSection cannot be called.");

        if (!_sectionPresent.TryGetValue(section.Id, out var presence))
            throw new InvalidOperationException(
                $"Section {section.Id} has no presence literal. Call CreateIntervalsForSection first.");

        var blockSizes = LabBlockSplitter.SplitLabIntoBlocks(section.LabHours, maxLabBlockHours);
        if (blockSizes.Count == 0)
            throw new InvalidOperationException(
                $"LabBlockSplitter returned empty for LabHours={section.LabHours}, max={maxLabBlockHours}.");

        var presenceBool = (BoolVar)presence;
        var intervals = new List<IntervalVar>(blockSizes.Count);
        var dayVars = new List<IntVar>(blockSizes.Count);

        // Lazy-build/validate dayPerSlot mapping (shared with GetOrCreateComponentDayVar).
        if (_dayPerSlotCache == null)
        {
            var arr = new long[timeSlots.Count];
            for (int i = 0; i < timeSlots.Count; i++)
                arr[i] = (long)timeSlots[i].DayOfWeek;
            _dayPerSlotCache = arr;
        }
        else if (_dayPerSlotCache.Length != timeSlots.Count)
        {
            throw new InvalidOperationException(
                $"timeSlots.Count ({timeSlots.Count}) does not match cached length ({_dayPerSlotCache.Length}). " +
                "DecisionVariables must not be reused across solves with different time-slot sets.");
        }

        for (int b = 0; b < blockSizes.Count; b++)
        {
            var blockHours = blockSizes[b];
            var start = CreateStartIntVar(
                duration: blockHours,
                timeSlots: timeSlots,
                varName: $"lab_start_s{section.Id}_b{b}",
                presence: presenceBool);
            var end = _model.NewIntVar(blockHours, _totalSlots, $"lab_end_s{section.Id}_b{b}");
            var iv = _model.NewOptionalIntervalVar(
                start,
                LinearExpr.Constant(blockHours),
                end,
                presenceBool,
                $"lab_iv_s{section.Id}_b{b}");
            intervals.Add(iv);

            // Per-block day var (different from GetOrCreateComponentDayVar which is one
            // day var per (section, component) pair). Multi-interval labs need N day vars.
            var dayVar = _model.NewIntVar(0, 6, $"lab_day_s{section.Id}_b{b}");
            _model.AddElement(start, _dayPerSlotCache, dayVar);
            dayVars.Add(dayVar);
        }

        // AllDifferent over day vars → each lab block lands on a different day.
        if (dayVars.Count > 1)
            _model.AddAllDifferent(dayVars);

        _labIntervalsList[section.Id] = intervals;

        // Backward-compat: _labIntervals (single-interval dict) tracks first block so
        // GetLabInterval / Plan 4-7 callers keep working for single-interval sections.
        // Multi-interval consumers MUST use GetLabIntervals.
        _labIntervals[section.Id] = intervals[0];
    }

    /// <summary>
    /// Plan 7 — Creates the component-start IntVar. If timeSlots is non-null, validates and
    /// restricts the domain to valid block starts (cross-day + slot-gap aware). If timeSlots
    /// is null, falls back to the legacy <c>NewIntVar(0, totalSlots - duration)</c> domain.
    /// If validation produces an empty valid-starts list, forces presence to 0 (section drops).
    /// </summary>
    private IntVar CreateStartIntVar(
        int duration,
        IReadOnlyList<TimeSlotInput>? timeSlots,
        string varName,
        BoolVar presence)
    {
        if (timeSlots == null)
        {
            // Legacy backward-compat path (Plan 4-6 tests).
            return _model.NewIntVar(0, _totalSlots - duration, varName);
        }

        var validStarts = SlotBlockValidator.ComputeValidBlockStarts(timeSlots, duration);
        if (validStarts.Count == 0)
        {
            // No valid block → force presence to 0 (section drops).
            // Return a constant placeholder (start value is irrelevant when present=0).
            // Avoids negative-upper-bound trap when duration > _totalSlots.
            _model.Add(presence == 0);
            return _model.NewConstant(0);
        }

        var domain = Google.OrTools.Util.Domain.FromValues(validStarts.Select(i => (long)i).ToArray());
        return _model.NewIntVarFromDomain(domain, varName);
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

    /// <summary>
    /// Plan 7b Task 7 — Multi-interval lab accessor. LabHours &gt; maxLabBlockHours ile
    /// bölünmüş section'lar için tüm interval listesini döner.
    /// Tek-interval section'lar için (CreateIntervalsForSection üzerinden kayıtlı)
    /// tek-elemanlı liste döner — backward-compat için single → list shim.
    /// </summary>
    public IReadOnlyList<IntervalVar> GetLabIntervals(int sectionId)
    {
        if (_labIntervalsList.TryGetValue(sectionId, out var list)) return list;
        if (_labIntervals.TryGetValue(sectionId, out var single))
            return new[] { single };
        throw new KeyNotFoundException($"No lab intervals registered for section {sectionId}.");
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
    public IReadOnlyDictionary<int, IReadOnlyList<IntervalVar>> AllLabIntervalsLists => _labIntervalsList;
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

        // Plan 7b — guard the multi-interval × parallel-room combination (deferred to Plan 9).
        // Single-interval optional path covers only block 0; remaining blocks would have no
        // room constraint, silently producing an under-constrained model.
        if (componentType == ComponentType.Lab && _labIntervalsList.TryGetValue(sectionId, out var blocks)
            && blocks.Count > 1)
            throw new NotSupportedException(
                $"Section {sectionId}: parallel-room optional intervals are not yet supported for " +
                $"multi-interval lab splits ({blocks.Count} blocks). See Plan 9 polish.");

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

    private readonly Dictionary<(int sectionId, int classroomId, ComponentType type, int groupIndex), IntervalVar> _optionalIntervalsForGroups = new();
    private readonly Dictionary<(int sectionId, int classroomId, ComponentType type, int groupIndex), ILiteral> _presenceLiteralsForGroups = new();

    /// <summary>
    /// Plan 4 Task 5 — paralel lab support.
    /// Section'ın belirli derslikte belirli component için <b>belirli group'a</b> ait optional interval'ını oluşturur.
    /// Lecture için <paramref name="groupIndex"/> 0 olmalı (lecture'da group kavramı yok).
    /// Lab için <paramref name="groupIndex"/> ∈ [0, GetLabRooms(sectionId).Count).
    /// Presence literal: sectionPresent ∧ (labRooms[groupIndex] == classroomId).
    /// Idempotent: aynı (sectionId, classroomId, componentType, groupIndex) anahtarıyla tekrar çağrılırsa cache'lenmiş interval döner.
    /// </summary>
    public IntervalVar CreateOptionalRoomIntervalForGroup(
        int sectionId,
        int classroomId,
        ComponentType componentType,
        int duration,
        int groupIndex)
    {
        var key = (sectionId, classroomId, componentType, groupIndex);
        if (_optionalIntervalsForGroups.TryGetValue(key, out var existing))
            return existing;

        if (!_sectionPresent.TryGetValue(sectionId, out var sectionPresent))
            throw new InvalidOperationException(
                $"Section {sectionId} has no presence literal. Call CreateIntervalsForSection first.");

        // Plan 7b — same multi-interval × parallel-room guard as CreateOptionalRoomInterval.
        if (componentType == ComponentType.Lab && _labIntervalsList.TryGetValue(sectionId, out var multiBlocks)
            && multiBlocks.Count > 1)
            throw new NotSupportedException(
                $"Section {sectionId}: per-group optional intervals are not yet supported for " +
                $"multi-interval lab splits ({multiBlocks.Count} blocks). See Plan 9 polish.");

        IntVar roomVar;
        if (componentType == ComponentType.Lecture)
        {
            if (groupIndex != 0)
                throw new ArgumentOutOfRangeException(nameof(groupIndex), groupIndex,
                    "Lecture component supports only groupIndex=0.");
            roomVar = GetLectureRoom(sectionId);
        }
        else
        {
            var labRooms = GetLabRooms(sectionId);
            if (groupIndex < 0 || groupIndex >= labRooms.Count)
                throw new ArgumentOutOfRangeException(nameof(groupIndex), groupIndex,
                    $"groupIndex out of range for section {sectionId} (labRooms.Count={labRooms.Count}).");
            roomVar = labRooms[groupIndex];
        }

        var typeName = componentType.ToString().ToLowerInvariant();

        // Step 1: reify room equality for this specific group's IntVar
        var roomEqLit = _model.NewBoolVar($"room_eq_{typeName}_s{sectionId}_c{classroomId}_g{groupIndex}");
        _model.Add(roomVar == classroomId).OnlyEnforceIf(roomEqLit);
        _model.Add(roomVar != classroomId).OnlyEnforceIf(roomEqLit.Not());

        // Step 2: presence ⇔ (sectionPresent ∧ roomEqLit)
        var presence = _model.NewBoolVar($"present_{typeName}_s{sectionId}_c{classroomId}_g{groupIndex}");
        _model.AddBoolAnd(new ILiteral[] { sectionPresent, roomEqLit }).OnlyEnforceIf(presence);
        _model.AddBoolOr(new ILiteral[] { sectionPresent.Not(), roomEqLit.Not() }).OnlyEnforceIf(presence.Not());

        var sourceInterval = componentType == ComponentType.Lecture
            ? GetLectureInterval(sectionId)
            : GetLabInterval(sectionId);
        if (sourceInterval == null)
            throw new InvalidOperationException(
                $"Section {sectionId} has no {componentType} interval; cannot create optional room interval.");

        var optInterval = _model.NewOptionalIntervalVar(
            sourceInterval.StartExpr(),
            LinearExpr.Constant(duration),
            sourceInterval.EndExpr(),
            presence,
            $"opt_iv_{typeName}_s{sectionId}_c{classroomId}_g{groupIndex}");

        _optionalIntervalsForGroups[key] = optInterval;
        _presenceLiteralsForGroups[key] = presence;
        return optInterval;
    }

    public IReadOnlyDictionary<(int, int, ComponentType, int), IntervalVar> AllOptionalIntervalsForGroups => _optionalIntervalsForGroups;
    public IReadOnlyDictionary<(int, int, ComponentType, int), ILiteral> AllPresenceLiteralsForGroups => _presenceLiteralsForGroups;

    private readonly Dictionary<(int sectionId, int classroomId, int blockIndex, int groupIndex), IntervalVar>
        _optionalLabBlockIntervals = new();
    private readonly Dictionary<(int sectionId, int classroomId, int blockIndex, int groupIndex), ILiteral>
        _presenceLiteralsForLabBlocks = new();

    /// <summary>
    /// Plan 7b Task 8 — Multi-interval lab support. Like
    /// <see cref="CreateOptionalRoomIntervalForGroup"/> but takes an explicit
    /// <paramref name="sourceInterval"/> + <paramref name="duration"/>, so each block of a
    /// time-split lab can register its own optional per-classroom interval (each block has
    /// its own start/end). Single-block (LabHours ≤ MaxLabBlockHours) sections continue to
    /// use <see cref="CreateOptionalRoomIntervalForGroup"/>; multi-block sections route
    /// through this helper.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Multi-interval × LabGroupCount > 1 (parallel rooms × time-split) is deferred to Plan 9.
    /// The encoder must guard the combination upstream — this helper itself does not, so the
    /// per-block, per-group, per-classroom registrations remain composable when Plan 9 lands.
    /// </para>
    /// <para>
    /// <b>Caller invariant:</b> <paramref name="sourceInterval"/> MUST equal
    /// <c>GetLabIntervals(sectionId)[blockIndex]</c>. The helper does not re-look-up the
    /// interval (to avoid repeated dictionary lookups in the encoder hot path), so passing
    /// a different interval would silently produce an under-constrained model.
    /// </para>
    /// </remarks>
    public IntervalVar CreateOptionalRoomIntervalForLabBlock(
        int sectionId,
        int classroomId,
        int blockIndex,
        int groupIndex,
        IntervalVar sourceInterval,
        int duration)
    {
        ArgumentNullException.ThrowIfNull(sourceInterval);
        var key = (sectionId, classroomId, blockIndex, groupIndex);
        if (_optionalLabBlockIntervals.TryGetValue(key, out var existing))
            return existing;

        if (!_sectionPresent.TryGetValue(sectionId, out var sectionPresent))
            throw new InvalidOperationException(
                $"Section {sectionId} has no presence literal. Call CreateIntervalsForSection first.");

        // Caller invariant guard: sourceInterval must be the blockIndex'th lab interval.
        // Debug.Assert keeps the hot path free in release builds while catching encoder bugs in tests.
        if (_labIntervalsList.TryGetValue(sectionId, out var registered) && blockIndex < registered.Count)
            System.Diagnostics.Debug.Assert(
                ReferenceEquals(sourceInterval, registered[blockIndex]),
                $"sourceInterval mismatch for section {sectionId}, blockIndex {blockIndex}: " +
                "caller must pass GetLabIntervals(sectionId)[blockIndex].");

        var labRooms = GetLabRooms(sectionId);
        if (groupIndex < 0 || groupIndex >= labRooms.Count)
            throw new ArgumentOutOfRangeException(nameof(groupIndex), groupIndex,
                $"groupIndex out of range for section {sectionId} (labRooms.Count={labRooms.Count}).");

        var roomVar = labRooms[groupIndex];
        var roomEqLit = _model.NewBoolVar(
            $"room_eq_lab_s{sectionId}_c{classroomId}_b{blockIndex}_g{groupIndex}");
        _model.Add(roomVar == classroomId).OnlyEnforceIf(roomEqLit);
        _model.Add(roomVar != classroomId).OnlyEnforceIf(roomEqLit.Not());

        var presence = _model.NewBoolVar(
            $"present_lab_s{sectionId}_c{classroomId}_b{blockIndex}_g{groupIndex}");
        _model.AddBoolAnd(new ILiteral[] { sectionPresent, roomEqLit }).OnlyEnforceIf(presence);
        _model.AddBoolOr(new ILiteral[] { sectionPresent.Not(), roomEqLit.Not() }).OnlyEnforceIf(presence.Not());

        var optInterval = _model.NewOptionalIntervalVar(
            sourceInterval.StartExpr(),
            LinearExpr.Constant(duration),
            sourceInterval.EndExpr(),
            presence,
            $"opt_iv_lab_s{sectionId}_c{classroomId}_b{blockIndex}_g{groupIndex}");

        _optionalLabBlockIntervals[key] = optInterval;
        _presenceLiteralsForLabBlocks[key] = presence;
        return optInterval;
    }

    public IReadOnlyDictionary<(int, int, int, int), IntervalVar> AllOptionalLabBlockIntervals => _optionalLabBlockIntervals;
    public IReadOnlyDictionary<(int, int, int, int), ILiteral> AllPresenceLiteralsForLabBlocks => _presenceLiteralsForLabBlocks;

    private readonly Dictionary<(int sectionId, ComponentType componentType), IntVar> _componentDayVars = new();
    private long[]? _dayPerSlotCache;

    /// <summary>
    /// Plan 5 — H-DL/H-DO/H15/H16 için ortak helper. Section'ın belirli component'inin (lecture/lab)
    /// hangi gün başladığını temsil eden IntVar (0..6, DayOfWeek enum int değeri).
    /// Idempotent: aynı (sectionId, componentType) için tek IntVar oluşturur.
    /// timeSlots → day-of-week mapping array tek kez oluşturulur ve cache'lenir.
    /// Not: timeSlots referansının DecisionVariables instance ömrü boyunca sabit olduğu varsayılır
    /// (ScheduleModelBuilder.Build içinde tek bir solve scope'unda kullanılır).
    /// </summary>
    public IntVar GetOrCreateComponentDayVar(int sectionId, ComponentType componentType,
        IReadOnlyList<TimeSlotInput> timeSlots)
    {
        var key = (sectionId, componentType);
        if (_componentDayVars.TryGetValue(key, out var existing))
            return existing;

        if (timeSlots.Count == 0)
            throw new InvalidOperationException("TimeSlots is empty; cannot create day variable.");

        // Lazy-build dayPerSlot mapping array (single allocation per DecisionVariables instance).
        if (_dayPerSlotCache == null)
        {
            var arr = new long[timeSlots.Count];
            for (int i = 0; i < timeSlots.Count; i++)
                arr[i] = (long)timeSlots[i].DayOfWeek;
            _dayPerSlotCache = arr;
        }
        else if (_dayPerSlotCache.Length != timeSlots.Count)
        {
            // Defensive: if a future caller ever reuses this DecisionVariables instance with a
            // different time-slot set, AddElement would silently use a stale day mapping.
            // Fail loudly instead.
            throw new InvalidOperationException(
                $"timeSlots.Count ({timeSlots.Count}) does not match cached length ({_dayPerSlotCache.Length}). " +
                "DecisionVariables must not be reused across solves with different time-slot sets.");
        }

        var sourceInterval = componentType == ComponentType.Lecture
            ? GetLectureInterval(sectionId)
            : GetLabInterval(sectionId);
        if (sourceInterval == null)
            throw new InvalidOperationException(
                $"Section {sectionId} has no {componentType} interval; cannot create day var.");

        var typeName = componentType.ToString().ToLowerInvariant();
        var startIdx = _model.NewIntVar(0, timeSlots.Count - 1, $"{typeName}_start_idx_s{sectionId}");
        _model.Add(startIdx == sourceInterval.StartExpr());

        var dayVar = _model.NewIntVar(0, 6, $"{typeName}_day_s{sectionId}");
        _model.AddElement(startIdx, _dayPerSlotCache, dayVar);

        _componentDayVars[key] = dayVar;
        return dayVar;
    }

    public IReadOnlyDictionary<(int, ComponentType), IntVar> AllComponentDayVars => _componentDayVars;
}
