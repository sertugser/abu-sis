using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H15 — Maximum Consecutive Hours per Instructor per Day.
/// Hoca aynı gün içinde aralıksız <see cref="_maxHoursPerDay"/> saatten fazla ders veremez.
///
/// Plan 5 Task 8b — slot coverage matrix sliding window:
/// 1. Pre-compute consecutive slot blocks per day (gap ≤ 5 min between adjacent slots = same block).
/// 2. Per (section, component, slotIdx) reify <c>covers ⇔ (start ≤ slotIdx) ∧ (slotIdx &lt; start + duration) ∧ present</c>.
/// 3. Per (instructor, consecutive block, sliding window of size <c>maxHours + 1</c>):
///    <c>sum(covers in window) ≤ maxHours</c> — equivalent to "≥ 1 free slot in any (maxHours+1)-window".
/// 4. Assumption literal per (instructor, window) — <c>secondarySectionId = first slot index of window</c>
///    so UnsatCoreInterpreter can attribute infeasibility to a specific window.
///
/// Performance: model size grows with <c>sections × totalSlots × instructors</c>. Typical
/// ~100 × 50 × 30 ≈ 150K reified BoolVars; CP-SAT handles within
/// <c>Schedule.OrTools.TimeLimitSeconds</c> (default 30s). If timeout regressions surface in
/// real-DB integration tests, fall back to a simpler daily-total bound.
///
/// <c>_maxHoursPerDay</c> SystemConfig.Schedule.MaxConsecutiveHoursPerDay'tan okunur (default 4),
/// SchedulingServiceCollectionExtensions DI factory tarafından enjekte edilir.
/// </summary>
public sealed class MaxConsecutiveHoursEncoder : IConstraintEncoder
{
    public string Code => "H15";
    public bool IsHard => true;

    private readonly int _maxHoursPerDay;
    private readonly int _breakDurationMinutes;

    public MaxConsecutiveHoursEncoder(int maxHoursPerDay = 4, int breakDurationMinutes = 5)
    {
        if (maxHoursPerDay <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHoursPerDay),
                maxHoursPerDay, "maxHoursPerDay must be positive.");
        if (breakDurationMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(breakDurationMinutes),
                breakDurationMinutes, "breakDurationMinutes must be non-negative.");
        _maxHoursPerDay = maxHoursPerDay;
        _breakDurationMinutes = breakDurationMinutes;
    }

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        // Plan 10 — Category toggle: admin H15'i "Tercihli" (Soft) yaparsa hard sliding-window
        // ban skip edilir; MaxConsecutiveHoursSoftEncoder devreye girer (penalty mod). Default Hard.
        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Hard");
        if (!string.Equals(category, "Hard", StringComparison.OrdinalIgnoreCase))
            return;

        if (ctx.Request.TimeSlots.Count == 0)
            return;

        // Step 1: pre-compute consecutive slot blocks per day. Each block is a list of slot
        // indices into ctx.Request.TimeSlots. Only blocks with size > maxHoursPerDay can violate.
        var consecutiveBlocks = BuildConsecutiveBlocks(ctx.Request.TimeSlots);
        if (consecutiveBlocks.Count == 0)
            return;

        // Step 2/3: per-instructor coverage matrix + sliding-window constraints.
        var byInstructor = ctx.Request.Sections
            .Where(s => s.InstructorId.HasValue)
            .GroupBy(s => s.InstructorId!.Value);

        foreach (var group in byInstructor)
        {
            var instructorId = group.Key;
            var sectionList = group.ToList();

            // coverageVars[(sectionId, ComponentType, slotIdx)] = BoolVar built lazily for sections
            // that actually contribute to ≥1 violating block.
            var coverageVars = new Dictionary<(int sectionId, ComponentType ct, int slotIdx), BoolVar>();

            foreach (var section in sectionList)
            {
                if (ctx.Variables.GetSectionPresent(section.Id) is not BoolVar present)
                    continue;

                if (section.TheoryHours > 0)
                {
                    var iv = ctx.Variables.GetLectureInterval(section.Id);
                    if (iv != null)
                        PopulateCoverage(ctx.Model, section.Id, ComponentType.Lecture,
                            iv, section.TheoryHours, present,
                            ctx.Request.TimeSlots.Count, coverageVars);
                }
                if (section.LabHours > 0)
                {
                    var iv = ctx.Variables.GetLabInterval(section.Id);
                    if (iv != null)
                        PopulateCoverage(ctx.Model, section.Id, ComponentType.Lab,
                            iv, section.LabHours, present,
                            ctx.Request.TimeSlots.Count, coverageVars);
                }
            }

            // Sliding window constraints per consecutive block.
            int windowSize = _maxHoursPerDay + 1;
            foreach (var block in consecutiveBlocks)
            {
                for (int start = 0; start + windowSize <= block.Count; start++)
                {
                    var windowSlots = new List<int>(windowSize);
                    for (int k = 0; k < windowSize; k++)
                        windowSlots.Add(block[start + k]);

                    var windowTerms = new List<LinearExpr>();
                    foreach (var section in sectionList)
                    {
                        foreach (var slotIdx in windowSlots)
                        {
                            if (coverageVars.TryGetValue(
                                    (section.Id, ComponentType.Lecture, slotIdx), out var lecCov))
                                windowTerms.Add(lecCov);
                            if (coverageVars.TryGetValue(
                                    (section.Id, ComponentType.Lab, slotIdx), out var labCov))
                                windowTerms.Add(labCov);
                        }
                    }

                    if (windowTerms.Count == 0) continue;

                    var literal = ctx.Assumptions.Register(Code,
                        primarySectionId: instructorId,
                        secondarySectionId: windowSlots[0]);
                    ctx.Model.Add(LinearExpr.Sum(windowTerms) <= _maxHoursPerDay)
                        .OnlyEnforceIf(literal);
                }
            }
        }
    }

    /// <summary>
    /// Groups slot indices into consecutive blocks per day, where consecutive means
    /// <c>nextStart - prevEnd ≤ BreakDurationMinutes</c> (matches v1 evaluator semantics).
    /// Only blocks with <c>count &gt; _maxHoursPerDay</c> are returned (smaller blocks can't violate).
    /// </summary>
    private List<List<int>> BuildConsecutiveBlocks(IReadOnlyList<TimeSlotInput> timeSlots)
    {
        var blocks = new List<List<int>>();

        // Group by day, preserving original index.
        var slotsByDay = timeSlots
            .Select((s, idx) => (slot: s, idx))
            .GroupBy(t => t.slot.DayOfWeek);

        foreach (var dayGroup in slotsByDay)
        {
            var ordered = dayGroup.OrderBy(t => t.slot.StartTime).ToList();
            if (ordered.Count == 0) continue;

            var current = new List<int> { ordered[0].idx };
            for (int k = 1; k < ordered.Count; k++)
            {
                var prevEnd = ordered[k - 1].slot.EndTime;
                var curStart = ordered[k].slot.StartTime;
                var gapMinutes = (curStart - prevEnd).TotalMinutes;
                if (gapMinutes <= _breakDurationMinutes)
                {
                    current.Add(ordered[k].idx);
                }
                else
                {
                    if (current.Count > _maxHoursPerDay) blocks.Add(current);
                    current = new List<int> { ordered[k].idx };
                }
            }
            if (current.Count > _maxHoursPerDay) blocks.Add(current);
        }

        return blocks;
    }

    /// <summary>
    /// Populates coverage matrix for a single (section, component) across all slot indices.
    /// <c>covers[i] ⇔ (start ≤ i) ∧ (i &lt; start + duration) ∧ present</c>.
    /// </summary>
    private static void PopulateCoverage(CpModel model, int sectionId, ComponentType ct,
        IntervalVar interval, int duration, BoolVar present, int totalSlots,
        Dictionary<(int, ComponentType, int), BoolVar> coverageVars)
    {
        var startExpr = interval.StartExpr();
        for (int i = 0; i < totalSlots; i++)
        {
            // startLeI ⇔ (start ≤ i)
            var startLeI = model.NewBoolVar($"h15_startLe_s{sectionId}_{ct}_i{i}");
            model.Add(startExpr <= i).OnlyEnforceIf(startLeI);
            model.Add(startExpr >= i + 1).OnlyEnforceIf(startLeI.Not());

            // iLtEnd ⇔ (i < start + duration), i.e. (start + duration ≥ i + 1)
            var iLtEnd = model.NewBoolVar($"h15_iLtEnd_s{sectionId}_{ct}_i{i}");
            model.Add(startExpr + duration >= i + 1).OnlyEnforceIf(iLtEnd);
            model.Add(startExpr + duration <= i).OnlyEnforceIf(iLtEnd.Not());

            // covers ⇔ startLeI ∧ iLtEnd ∧ present
            var covers = model.NewBoolVar($"h15_covers_s{sectionId}_{ct}_i{i}");
            model.AddBoolAnd(new ILiteral[] { startLeI, iLtEnd, present }).OnlyEnforceIf(covers);
            model.AddBoolOr(new ILiteral[] { startLeI.Not(), iLtEnd.Not(), present.Not() })
                .OnlyEnforceIf(covers.Not());

            coverageVars[(sectionId, ct, i)] = covers;
        }
    }
}
