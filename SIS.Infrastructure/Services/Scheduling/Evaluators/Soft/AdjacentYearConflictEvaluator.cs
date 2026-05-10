using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

/// <summary>
/// S20 — Komşu Yıl Çakışma SOFT Penalty.
///
/// Mantık: Aynı bölüm içinde |year_a - year_b| == 1 olan ders çiftlerinde modulo
/// eşleşmiş (sec_i ↔ sec_i) pair'ler. Az sayıda öğrenci (kalan/önden alan) iki dersi
/// birden alır; section seçim esnekliği var. HARD bloklamak gereksiz, SOFT penalty
/// solver'ı bu çakışmayı tercih ETMEMEYE yönlendirir.
///
/// Pair başına PenaltyPerPair eklenir (default 5.0). Çakışma sayısı arttıkça penalty
/// orantılı artar; SA/LNS bunu minimize etmeye çalışır.
/// </summary>
public sealed class AdjacentYearConflictEvaluator : IConstraintEvaluator
{
    public string Code => "S20";
    public string Name => "Komsu Yil Cakisma";
    public ConstraintCategory Category => ConstraintCategory.Soft;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var snapshot = context.Snapshot;
        var config = context.Config;

        if (!config.S20Enabled || c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        var pairs = snapshot.AdjacentYearSoftPairs;
        if (pairs == null || pairs.Count == 0)
            return ConstraintResult.Ok();

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var existing in snapshot.Sections)
        {
            if (existing.SectionId == c.SectionId) continue;
            if (existing.TimeBlocks.Count == 0) continue;

            var pair = c.SectionId < existing.SectionId
                ? (c.SectionId, existing.SectionId)
                : (existing.SectionId, c.SectionId);

            if (!pairs.Contains(pair)) continue;

            var overlaps = SchedulingHelpers.GetOverlapSlots(c.TimeBlocks, existing.TimeBlocks);
            if (overlaps.Count == 0) continue;

            totalPenalty += config.S20PenaltyPerPair;
            notes.Add(
                $"S20: '{c.CourseCode}' (yıl modulo eşi '{existing.CourseCode}') ile " +
                $"komşu yıl pair'i çakışıyor — kalan/önden alan öğrenciler için tercih edilmez.");
        }

        if (totalPenalty > 0)
            return new ConstraintResult { SoftPenalty = totalPenalty, Notes = notes };

        return ConstraintResult.Ok();
    }
}
