using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// LunchBreakSoftEncoder — H13 ConstraintDefinition'ın Category="Soft" (Tercihli) modu.
/// Lunch penceresine (default 11:40-12:20) denk düşen her section start slotu için
/// S5Weight penalty. v1 LunchBreakSoftEvaluator.cs:30-37 portu.
/// </summary>
/// <remarks>
/// Plan 10 — Category-aware encoder pipeline. Eski tasarımda S5 ayrı ConstraintDefinition
/// idi; Plan 10'da S5 row kaldırıldı, H13 toggle (Hard/Soft) bu encoder'ı yönetiyor:
///   - H13 IsEnabled=true + Category="Hard" → LunchBreakEncoder (yasak) çalışır, bu skip.
///   - H13 IsEnabled=true + Category="Soft" → bu encoder çalışır (penalty), hard skip.
///   - H13 IsEnabled=false → her ikisi de skip.
/// Pre-compute lunch-overlap-flagged slot indeksleri. Section start IntVar'ı bu
/// indekslerden biriyle eşleşirse reified literal aktif olur ve <c>S5Weight</c>
/// penalty objektif fonksiyona eklenir.
/// </remarks>
public sealed class LunchBreakSoftEncoder : IConstraintEncoder
{
    // Plan 10 — Code "S5" → "H13": tek ConstraintDefinition (H13) iki davranışı yönetir.
    public string Code => "H13";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        // Plan 10 — Category="Soft" (Tercihli) ise penalty modu. Default Hard → skip.
        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Hard");
        if (!string.Equals(category, "Soft", StringComparison.OrdinalIgnoreCase))
            return;

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

            // Lab interval check — iterate ALL blocks via GetLabIntervals (Plan 7b plural accessor).
            // Single-block sections fall through the single-element fallback list; multi-block
            // sections accrue per-block S5 penalty if any block lands in lunch window.
            // Pattern matches ClassroomConflictEncoder + SolutionMapper.
            try
            {
                var labIntervals = ctx.Variables.GetLabIntervals(sectionId);
                for (int b = 0; b < labIntervals.Count; b++)
                    AddLunchOverlapPenalty(
                        ctx, sectionId, $"lab_b{b}", labIntervals[b], lunchSlotIndices, t.S5Weight);
            }
            catch (KeyNotFoundException)
            {
                // Section has no lab component (theory-only). Skip.
            }
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
