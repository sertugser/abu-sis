using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S3 — SlotCongestion. Aynı slot'ta yerleşmiş section sayısı
/// <see cref="SoftThresholds.S3MaxSectionsPerSlot"/> eşiğini aşarsa
/// pen = (count - cap) × <see cref="SoftThresholds.S3Weight"/>.
/// v1 <c>SlotCongestionEvaluator.cs:33-42</c> portu — aggregate per-slot count.
///
/// Implementation: her slot indeksi için, her section'ın lecture + lab interval start'ları
/// o slot ile eşit mi reify edilir (slot literal = (start == slotIdx)). Slot başına
/// literal toplamı = count; excess = max(0, count - cap); ceza = excess × weight.
/// </summary>
/// <remarks>
/// Plan 9 Issue 4b — Lab intervals (plural) per-block reified literal eklenir. v1
/// <c>SlotCongestionEvaluator</c> ALL TimeBlocks (theory + lab) sayar; lab-only ve
/// multi-block lab section'lar artık v2'de de S3 count'a katkıda bulunur. Pre-fix lab-only
/// section'lar (TheoryHours=0) tamamen skip ediliyordu; multi-block lab section'lar yalnız
/// lecture component üzerinden katkıda bulunuyordu.
/// </remarks>
public sealed class SlotCongestionEncoder : IConstraintEncoder
{
    public string Code => "S3";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        var t = ctx.Request.SoftThresholds;
        if (t.S3Weight <= 0)
            return;

        var slotCount = ctx.Request.TimeSlots.Count;
        if (slotCount == 0)
            return;

        // For each slot, build sum of "section component starts at this slot" reified literals.
        for (int slotIdx = 0; slotIdx < slotCount; slotIdx++)
        {
            var slotLiterals = new List<BoolVar>();

            foreach (var (sectionId, _) in ctx.Variables.AllSectionPresent)
            {
                // Lecture component (Plan 8 baseline).
                var lectureInterval = ctx.Variables.GetLectureInterval(sectionId);
                if (lectureInterval != null)
                {
                    var lit = ctx.Model.NewBoolVar($"s3_lect_s{sectionId}_slot{slotIdx}");
                    ctx.Model.Add(lectureInterval.StartExpr() == slotIdx).OnlyEnforceIf(lit);
                    ctx.Model.Add(lectureInterval.StartExpr() != slotIdx).OnlyEnforceIf(lit.Not());
                    slotLiterals.Add(lit);
                }

                // Plan 9 Issue 4b — Lab intervals (plural). Lab-only section'lar
                // (TheoryHours=0) ve multi-block lab section'lar v1 paritesi için
                // her block bağımsız reified literal olarak count'a girer.
                // Pattern: ctx.Variables.GetLabIntervals throws when section has no lab
                // component → catch + skip (matches LunchBreakSoftEncoder + EarlyLateEncoder).
                try
                {
                    var labIvs = ctx.Variables.GetLabIntervals(sectionId);
                    for (int b = 0; b < labIvs.Count; b++)
                    {
                        var lit = ctx.Model.NewBoolVar($"s3_lab_s{sectionId}_b{b}_slot{slotIdx}");
                        ctx.Model.Add(labIvs[b].StartExpr() == slotIdx).OnlyEnforceIf(lit);
                        ctx.Model.Add(labIvs[b].StartExpr() != slotIdx).OnlyEnforceIf(lit.Not());
                        slotLiterals.Add(lit);
                    }
                }
                catch (KeyNotFoundException)
                {
                    // Theory-only section (no lab component). Skip.
                }
            }

            if (slotLiterals.Count == 0) continue;

            // count = sum(literals); excess = max(0, count - cap)
            var count = LinearExpr.Sum(slotLiterals.ToArray());
            var excess = ctx.Model.NewIntVar(0, slotLiterals.Count, $"s3_excess_slot{slotIdx}");
            ctx.Model.AddMaxEquality(excess, new LinearExpr[]
            {
                count - LinearExpr.Constant(t.S3MaxSectionsPerSlot),
                LinearExpr.Constant(0)
            });
            ctx.Penalties.Add(excess * t.S3Weight);
        }
    }
}
