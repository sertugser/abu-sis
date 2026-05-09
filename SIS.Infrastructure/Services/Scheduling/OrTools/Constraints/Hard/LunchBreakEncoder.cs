namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H13 — Lunch Break. Section'lar öğle arası saatlerinde başlayamaz.
///
/// Lunch start/end SystemConfig'ten okunur (Schedule.LunchBreakStart / Schedule.LunchBreakEnd, default 11:40-12:20).
/// Encoder.Encode sync olduğu için config TimeOnly'lar constructor'a enjekte edilir;
/// DI factory'de SchedulingServiceCollectionExtensions tarafından okunur.
///
/// Admin lunch saatlerini DB'den değiştirebilir → 5 dk cache sonra etkili.
///
/// CP-SAT model: TimeSlots arasında StartTime ∈ [lunchStart, lunchEnd) olanları belirle.
/// Section'ın start indeksi bu slot'lara eşit olamaz: Add(start != slotIndex).OnlyEnforceIf(literal).
/// </summary>
public sealed class LunchBreakEncoder : IConstraintEncoder
{
    public string Code => "H13";
    public bool IsHard => true;

    private readonly TimeOnly _lunchStart;
    private readonly TimeOnly _lunchEnd;

    public LunchBreakEncoder(TimeOnly lunchStart, TimeOnly lunchEnd)
    {
        if (lunchEnd <= lunchStart)
            throw new ArgumentException(
                $"Lunch end ({lunchEnd}) must be after lunch start ({lunchStart}).",
                nameof(lunchEnd));
        _lunchStart = lunchStart;
        _lunchEnd = lunchEnd;
    }

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        // Plan 10 — Category toggle: admin H13'ü "Tercihli" (Soft) yaparsa hard ban
        // skip edilir; LunchBreakSoftEncoder devreye girer (penalty mod). Default Hard.
        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Hard");
        if (!string.Equals(category, "Hard", StringComparison.OrdinalIgnoreCase))
            return;

        // Find slot indices whose start time falls within [lunchStart, lunchEnd)
        var lunchSlotIndices = new List<int>();
        for (int i = 0; i < ctx.Request.TimeSlots.Count; i++)
        {
            var slot = ctx.Request.TimeSlots[i];
            if (slot.StartTime >= _lunchStart && slot.StartTime < _lunchEnd)
                lunchSlotIndices.Add(i);
        }

        if (lunchSlotIndices.Count == 0)
            return;  // no lunch slots in this term's grid

        // For each section's lecture/lab interval, forbid start at lunch slot indices
        foreach (var section in ctx.Request.Sections)
        {
            if (section.TheoryHours > 0)
            {
                var iv = ctx.Variables.GetLectureInterval(section.Id);
                if (iv != null)
                {
                    foreach (var lunchIdx in lunchSlotIndices)
                    {
                        var literal = ctx.Assumptions.Register(Code, primarySectionId: section.Id, secondarySectionId: lunchIdx);
                        ctx.Model.Add(iv.StartExpr() != lunchIdx).OnlyEnforceIf(literal);
                    }
                }
            }

            if (section.LabHours > 0)
            {
                // Plan 9 Issue 3 — multi-block lab parity (Plan 7b Task 7 plural API).
                // Pre-fix used singular GetLabInterval which only returns block 0; non-primary
                // blocks (LabHours > MaxLabBlockHours, e.g. LabHours=4 / max=2 → 2 blocks)
                // could land in lunch slots. Iterate ALL blocks via GetLabIntervals so every
                // block start avoids each lunch slot index. Pattern matches LunchBreakSoftEncoder
                // (Plan 8 Task 6) and ClassroomConflictEncoder (Plan 7b Task 7).
                var labIntervals = ctx.Variables.GetLabIntervals(section.Id);
                foreach (var iv in labIntervals)
                {
                    foreach (var lunchIdx in lunchSlotIndices)
                    {
                        var literal = ctx.Assumptions.Register(Code,
                            primarySectionId: section.Id, secondarySectionId: lunchIdx);
                        ctx.Model.Add(iv.StartExpr() != lunchIdx).OnlyEnforceIf(literal);
                    }
                }
            }
        }
    }
}
