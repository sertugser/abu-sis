using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S5 — LunchBreakSoft. Lunch penceresine (default 11:40-12:20) denk düşen
/// her section start slotu için S5Weight penalty. v1 LunchBreakSoftEvaluator.cs:30-37 portu.
/// </summary>
/// <remarks>
/// Yalnızca H13 LunchBreak hard değilse anlamlıdır (v1 davranışı). v2'de H13 hard
/// ise lunch slot'larına yerleşim zaten yasaktır; S5 sessizce 0 ceza üretir.
/// Pre-compute lunch-overlap-flagged slot indeksleri. Section start IntVar'ı bu
/// indekslerden biriyle eşleşirse reified literal aktif olur ve <c>S5Weight</c>
/// penalty objektif fonksiyona eklenir.
/// </remarks>
public sealed class LunchBreakSoftEncoder : IConstraintEncoder
{
    public string Code => "S5";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;
        var t = ctx.Request.SoftThresholds;
        if (t.S5Weight <= 0) return;

        // Pre-compute slot indices that overlap lunch window.
        // overlap iff: slot.StartTime < lunchEnd && slot.EndTime > lunchStart
        var lunchSlotIndices = new List<int>();
        for (int i = 0; i < ctx.Request.TimeSlots.Count; i++)
        {
            var slot = ctx.Request.TimeSlots[i];
            if (slot.StartTime < t.S5LunchBreakEnd && slot.EndTime > t.S5LunchBreakStart)
                lunchSlotIndices.Add(i);
        }
        if (lunchSlotIndices.Count == 0) return;

        foreach (var (sectionId, _) in ctx.Variables.AllSectionPresent)
        {
            // Lecture interval check
            var lecture = ctx.Variables.GetLectureInterval(sectionId);
            if (lecture != null)
                AddLunchOverlapPenalty(ctx, sectionId, "lec", lecture, lunchSlotIndices, t.S5Weight);

            // Lab interval check (single + first multi-block).
            // NOTE: Plan 9 polish — multi-block lab intervals (Plan 7b's GetLabIntervals
            // list) are NOT iterated here; only the primary GetLabInterval is checked.
            // Per-block S5 penalty for parallel/multi-interval labs deferred to Plan 9.
            var lab = ctx.Variables.GetLabInterval(sectionId);
            if (lab != null)
                AddLunchOverlapPenalty(ctx, sectionId, "lab", lab, lunchSlotIndices, t.S5Weight);
        }
    }

    private static void AddLunchOverlapPenalty(
        EncoderContext ctx,
        int sectionId,
        string componentTag,
        IntervalVar interval,
        IReadOnlyList<int> lunchSlotIndices,
        int weight)
    {
        // Per-lunch-slot reified literal: lit_idx = (start == idx).
        // Sum of (lit_idx × weight) gives the total S5 penalty contribution for this interval.
        foreach (var idx in lunchSlotIndices)
        {
            var lit = ctx.Model.NewBoolVar($"s5_{componentTag}_s{sectionId}_slot{idx}");
            ctx.Model.Add(interval.StartExpr() == idx).OnlyEnforceIf(lit);
            ctx.Model.Add(interval.StartExpr() != idx).OnlyEnforceIf(lit.Not());
            ctx.Penalties.Add(LinearExpr.Term(lit, (long)weight));
        }
    }
}
