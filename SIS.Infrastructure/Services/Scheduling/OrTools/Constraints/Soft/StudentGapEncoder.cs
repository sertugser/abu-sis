namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S5 + S7 — Student Day-Internal Gaps. Öğrencinin bir gün içindeki ders programında
/// uzun boşluklar (S5) ve aralıksız çok ders (S7) olmasın. İki kısıt birleşik encoder'da
/// taşındı çünkü her ikisi de aynı per-(student, day) slot dizisi üzerinden hesaplanır.
/// PLAN 3 SCOPE: Diagnostic stub. ConstraintFlags["S5_S7"]=false ise hiçbir şey yapmaz;
/// true ise de Plan 3'te penalty matematiği eklenmemiş — early return.
/// PLAN 4 SCOPE: Real penalty implementation (per-(student, day) slot pattern → gap minutes
/// + consecutive-slot run length penalty).
/// </summary>
public sealed class StudentGapEncoder : IConstraintEncoder
{
    public string Code => "S5_S7";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        // Plan 3 diagnostic stub. Plan 4 will compute student day-internal gap + consecutive-run penalty.
        // No penalty added; no assumption literal (soft constraints aren't UNSAT-core diagnostics).
    }
}
