using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S3 — SlotCongestion. Aynı slot'ta yerleşmiş section sayısı
/// <see cref="SoftThresholds.S3MaxSectionsPerSlot"/> eşiğini aşarsa
/// pen = (count - cap) × <see cref="SoftThresholds.S3Weight"/>.
/// v1 <c>SlotCongestionEvaluator.cs:33-42</c> portu — aggregate per-slot count.
///
/// Implementation: her slot indeksi için, her section'ın lecture interval start'ı
/// o slot ile eşit mi reify edilir (slot literal = (start == slotIdx)). Slot başına
/// literal toplamı = count; excess = max(0, count - cap); ceza = excess × weight.
/// </summary>
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

        // For each slot, build sum of "section starts at this slot" reified literals.
        for (int slotIdx = 0; slotIdx < slotCount; slotIdx++)
        {
            var slotLiterals = new List<BoolVar>();

            foreach (var (sectionId, _) in ctx.Variables.AllSectionPresent)
            {
                // NOTE: Plan 9 — lab-only sections (TheoryHours=0, LabHours>0) currently
                // skipped because GetLectureInterval returns null. v1 SlotCongestionEvaluator
                // counts ALL TimeBlocks (theory + lab). Plan 9 polish will iterate lab
                // intervals (single + parallel + multi-block) for full v1 parity.
                var lectureInterval = ctx.Variables.GetLectureInterval(sectionId);
                if (lectureInterval == null) continue;

                // Reify: lit = (start == slotIdx)
                var lit = ctx.Model.NewBoolVar($"s3_section{sectionId}_slot{slotIdx}");
                ctx.Model.Add(lectureInterval.StartExpr() == slotIdx).OnlyEnforceIf(lit);
                ctx.Model.Add(lectureInterval.StartExpr() != slotIdx).OnlyEnforceIf(lit.Not());
                slotLiterals.Add(lit);
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

        // NOTE: Lab intervals intentionally NOT iterated. Multi-block lab + S3 interaction
        // (per-block congestion contribution) is deferred to Plan 9 polish — consistent with
        // Task 4 (S1 OccupancyEncoder) scope. Single-component lecture sections cover the
        // primary v1 SlotCongestionEvaluator semantics.
    }
}
