using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S21 Online Slot Tercihi (Soft) — Online derslerin OnlineOnly slot'a yerleşmesini teşvik eder.
///
/// Mantık: Online section'ın (Lecture + Lab blok'larının) her start slot'u **OnlineOnly DEĞİL** bir
/// slot'a düşerse <see cref="SoftThresholds.S21Weight"/> penalty objektif fonksiyona eklenir.
/// Solver penalty'yi minimize ettiği için OnlineOnly slot'u tercih eder; yetersiz ise Both'a düşer.
///
/// Kapsam:
///   - Online dersler (TeachingMethod=Online) — penalty alır
///   - FaceToFace dersler — kapsam dışı (zaten H17 onları OnlineOnly slot'a sokmaz)
///   - Hybrid dersler — kapsam dışı (her slot tipi muafiyetli)
///
/// Pattern: <see cref="LunchBreakSoftEncoder"/> ile aynı (pre-compute slot indeksleri + reified literal).
/// </summary>
public sealed class OnlineSlotPreferenceEncoder : IConstraintEncoder
{
    public string Code => "S21";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Soft");
        if (!string.Equals(category, "Soft", StringComparison.OrdinalIgnoreCase))
            return;

        var weight = ctx.Request.SoftThresholds.S21Weight;
        if (weight <= 0) return;

        // Pre-compute: penalty verilen slot indeksleri (= OnlineOnly olmayan slot'lar).
        // FaceToFaceOnly zaten H17 ile yasak; Both slot'lara Online ders düşerse penalty.
        var penaltySlotIndices = new List<int>();
        for (int i = 0; i < ctx.Request.TimeSlots.Count; i++)
        {
            var mode = ctx.Request.TimeSlots[i].AllowedMode;
            if (mode != TimeSlotAllowedMode.OnlineOnly)
                penaltySlotIndices.Add(i);
        }
        if (penaltySlotIndices.Count == 0) return;

        // Sadece Online section'ları kapsa.
        var onlineSections = ctx.Request.Sections
            .Where(s => s.TeachingMethod == TeachingMethod.Online)
            .ToList();
        if (onlineSections.Count == 0) return;

        foreach (var section in onlineSections)
        {
            // Lecture interval
            var lecture = ctx.Variables.GetLectureInterval(section.Id);
            if (lecture != null)
                AddSlotPenalty(ctx, section.Id, "lec", lecture, penaltySlotIndices, weight);

            // Lab interval(s) — multi-block lab desteği (LunchBreakSoftEncoder pattern'i).
            try
            {
                var labIntervals = ctx.Variables.GetLabIntervals(section.Id);
                for (int b = 0; b < labIntervals.Count; b++)
                    AddSlotPenalty(ctx, section.Id, $"lab_b{b}", labIntervals[b], penaltySlotIndices, weight);
            }
            catch (KeyNotFoundException)
            {
                // Section has no lab component — skip.
            }
        }
    }

    private static void AddSlotPenalty(
        EncoderContext ctx,
        int sectionId,
        string componentTag,
        IntervalVar interval,
        IReadOnlyList<int> penaltySlotIndices,
        int weight)
    {
        // Her penalty-verilen slot için reified literal: lit_idx = (start == idx).
        // Online section'ın start slot'u o slot index'e eşitse penalty eklenir.
        foreach (var idx in penaltySlotIndices)
        {
            var lit = ctx.Model.NewBoolVar($"s21_{componentTag}_s{sectionId}_slot{idx}");
            ctx.Model.Add(interval.StartExpr() == idx).OnlyEnforceIf(lit);
            ctx.Model.Add(interval.StartExpr() != idx).OnlyEnforceIf(lit.Not());
            ctx.Penalties.Add(LinearExpr.Term(lit, (long)weight));
        }
    }
}
