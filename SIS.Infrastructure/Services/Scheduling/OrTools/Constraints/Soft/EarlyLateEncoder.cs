namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S9 — Early/Late Slot Penalty. 17:00 sonrası (akşam) slot'lara verilen ders'e penalty —
/// öğrenci ve hoca tercihi. Çok erken slot'lar da ileride aynı encoder ile cezalandırılabilir.
/// PLAN 3 SCOPE: Diagnostic stub. ConstraintFlags["S9"]=false ise hiçbir şey yapmaz;
/// true ise de Plan 3'te penalty matematiği eklenmemiş — early return.
/// PLAN 4 SCOPE: Real penalty implementation (per-slot late/early-of-day weight,
/// config'den okunan eşik saat).
/// </summary>
public sealed class EarlyLateEncoder : IConstraintEncoder
{
    public string Code => "S9";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        // Plan 3 diagnostic stub. Plan 4 will compute early/late slot penalty per assignment.
        // No penalty added; no assumption literal (soft constraints aren't UNSAT-core diagnostics).
    }
}
