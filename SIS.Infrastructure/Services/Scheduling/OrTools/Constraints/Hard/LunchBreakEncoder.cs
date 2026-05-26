using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H13 — Lunch Break. Section'ların blokları öğle arası slot'larını KAPSAYAMAZ (yalnız "başlayamaz"
/// değil — span overlap; H13_LunchBreakRule evaluator'ı ile parite).
///
/// Lunch start/end SystemConfig'ten okunur (Schedule.LunchBreakStart / Schedule.LunchBreakEnd, default 11:40-12:20).
/// Encoder.Encode sync olduğu için config TimeOnly'lar constructor'a enjekte edilir;
/// DI factory'de SchedulingServiceCollectionExtensions tarafından okunur.
///
/// Admin lunch saatlerini DB'den değiştirebilir → 5 dk cache sonra etkili.
///
/// CP-SAT model: TimeSlots arasında StartTime ∈ [lunchStart, lunchEnd) olan lunch slot'larını belirle.
/// Bir blok ([start, start+duration)) bir lunch slot'unu kapsarsa start ∈ [lunchIdx-duration+1, lunchIdx];
/// bu start aralığı (aynı güne clamp'li) yasaklanır → blok öğle arasını es geçemez.
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

        // Lunch slot indeksleri + her günün ilk global indeksi (start aralığını aynı güne clamp için).
        var slots = ctx.Request.TimeSlots;
        var lunchSlotIndices = new List<int>();
        var dayFirstIndex = new Dictionary<DayOfWeek, int>();
        for (int i = 0; i < slots.Count; i++)
        {
            if (!dayFirstIndex.ContainsKey(slots[i].DayOfWeek))
                dayFirstIndex[slots[i].DayOfWeek] = i;
            if (slots[i].StartTime >= _lunchStart && slots[i].StartTime < _lunchEnd)
                lunchSlotIndices.Add(i);
        }

        if (lunchSlotIndices.Count == 0)
            return;  // no lunch slots in this term's grid

        foreach (var section in ctx.Request.Sections)
        {
            if (section.TheoryHours > 0)
            {
                var iv = ctx.Variables.GetLectureInterval(section.Id);
                if (iv != null)
                    ForbidCovering(ctx, section.Id, iv, section.TheoryHours, lunchSlotIndices, dayFirstIndex, slots);
            }

            if (section.LabHours > 0)
            {
                // Multi-block lab: her blok kendi süresiyle değerlendirilir (LabBlockSplitter ile aynı bölünme).
                var labIntervals = ctx.Variables.GetLabIntervals(section.Id);
                var blockSizes = LabBlockSplitter.SplitLabIntoBlocks(section.LabHours, ctx.Request.MaxLabBlockHours);
                for (int b = 0; b < labIntervals.Count; b++)
                {
                    var duration = b < blockSizes.Count ? blockSizes[b] : 1;
                    ForbidCovering(ctx, section.Id, labIntervals[b], duration, lunchSlotIndices, dayFirstIndex, slots);
                }
            }
        }
    }

    /// <summary>
    /// Bloğun ([start, start+duration)) herhangi bir lunch slot'unu kapsamasını yasaklar.
    /// Blok lunchIdx'i kapsarsa start ∈ [lunchIdx-duration+1, lunchIdx]; bu aralık lunchIdx'in
    /// gününün başına clamp'lenir (önceki günün başlangıçları tek-gün blok kuralı gereği bu lunch
    /// slot'unu zaten kapsayamaz, onları yasaklamak meşru blokları engellerdi). Tüm aralık tek
    /// assumption literal'i altında gated edilir (UNSAT diagnostiği için per (section, lunchIdx)).
    /// </summary>
    private void ForbidCovering(
        EncoderContext ctx, int sectionId, IntervalVar iv, int duration,
        IReadOnlyList<int> lunchSlotIndices, IReadOnlyDictionary<DayOfWeek, int> dayFirstIndex,
        IReadOnlyList<TimeSlotInput> slots)
    {
        foreach (var lunchIdx in lunchSlotIndices)
        {
            var dayFirst = dayFirstIndex[slots[lunchIdx].DayOfWeek];
            var lowStart = Math.Max(dayFirst, lunchIdx - duration + 1);
            var literal = ctx.Assumptions.Register(Code, primarySectionId: sectionId, secondarySectionId: lunchIdx);
            for (int s = lowStart; s <= lunchIdx; s++)
                ctx.Model.Add(iv.StartExpr() != s).OnlyEnforceIf(literal);
        }
    }
}
