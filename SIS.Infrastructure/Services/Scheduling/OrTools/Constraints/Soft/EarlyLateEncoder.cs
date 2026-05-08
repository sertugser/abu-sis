using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S9 — EarlyLate. Section start slotu MorningStart ≤ veya EveningStart ≥ ise her slot için
/// S9Weight penalty. v1 EarlyLateEvaluator.cs:23-35 portu (per-slot penalty 6 default).
/// </summary>
/// <remarks>
/// Pre-compute early/late-flagged slot indeksleri. Section'ın lecture ve lab interval(ler)i
/// bu indekslerden biriyle eşleşirse reified literal aktif olur ve <c>S9Weight</c> penalty
/// objektif fonksiyona eklenir. Lab için <see cref="DecisionVariables.GetLabIntervals"/>
/// (plural) kullanılır — multi-block lab parity için Plan 7b/Plan 8 Task 6 deseni.
/// NOTE: Plan 9 H13 hard variant (early/late blok yasakları) bu soft encoder ile simetriktir;
/// hard çalışırsa S9 sessizce 0 ceza üretir (early/late slot zaten yasaklı).
/// </remarks>
public sealed class EarlyLateEncoder : IConstraintEncoder
{
    public string Code => "S9";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;
        var t = ctx.Request.SoftThresholds;
        if (t.S9Weight <= 0) return;

        // Pre-compute slot indices that are early (≤ MorningStart) or late (≥ EveningStart).
        var earlyLateIndices = new List<int>();
        for (int i = 0; i < ctx.Request.TimeSlots.Count; i++)
        {
            var slot = ctx.Request.TimeSlots[i];
            if (slot.StartTime <= t.S9MorningStart || slot.StartTime >= t.S9EveningStart)
                earlyLateIndices.Add(i);
        }
        if (earlyLateIndices.Count == 0) return;

        foreach (var (sectionId, _) in ctx.Variables.AllSectionPresent)
        {
            // Lecture interval check
            var lecture = ctx.Variables.GetLectureInterval(sectionId);
            if (lecture != null)
                AddPerSlotPenalty(ctx, sectionId, "lec", lecture, earlyLateIndices, t.S9Weight);

            // Lab interval check — iterate ALL blocks via GetLabIntervals (Plan 7b plural accessor).
            // Multi-block sections accrue per-block S9 penalty if any block lands in early/late slot.
            // Pattern matches LunchBreakSoftEncoder + ClassroomConflictEncoder + SolutionMapper.
            try
            {
                var labIntervals = ctx.Variables.GetLabIntervals(sectionId);
                for (int b = 0; b < labIntervals.Count; b++)
                    AddPerSlotPenalty(
                        ctx, sectionId, $"lab_b{b}", labIntervals[b], earlyLateIndices, t.S9Weight);
            }
            catch (KeyNotFoundException)
            {
                // Section has no lab component (theory-only). Skip.
            }
        }
    }

    private static void AddPerSlotPenalty(
        EncoderContext ctx,
        int sectionId,
        string componentTag,
        IntervalVar interval,
        IReadOnlyList<int> indices,
        int weight)
    {
        // Per-flagged-slot reified literal: lit_idx = (start == idx).
        // Sum of (lit_idx × weight) gives the total S9 penalty contribution for this interval.
        foreach (var idx in indices)
        {
            var lit = ctx.Model.NewBoolVar($"s9_{componentTag}_s{sectionId}_slot{idx}");
            ctx.Model.Add(interval.StartExpr() == idx).OnlyEnforceIf(lit);
            ctx.Model.Add(interval.StartExpr() != idx).OnlyEnforceIf(lit.Not());
            ctx.Penalties.Add(LinearExpr.Term(lit, (long)weight));
        }
    }
}
