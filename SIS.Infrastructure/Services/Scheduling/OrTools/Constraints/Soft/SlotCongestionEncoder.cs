namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S3 — Slot Congestion. Slot başına dolu derslik sayısı limiti — aynı slotta çok fazla
/// section çakışırsa kampüs kaynakları (otopark, kantin, koridor) yüklenir.
/// PLAN 3 SCOPE: Diagnostic stub. ConstraintFlags["S3"]=false ise hiçbir şey yapmaz;
/// true ise de Plan 3'te penalty matematiği eklenmemiş — early return.
/// PLAN 4 SCOPE: Real penalty implementation (per-slot occupied-classroom count vs configured cap).
/// </summary>
public sealed class SlotCongestionEncoder : IConstraintEncoder
{
    public string Code => "S3";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        // Plan 3 diagnostic stub. Plan 4 will compute congestion penalty per slot.
        // No penalty added; no assumption literal (soft constraints aren't UNSAT-core diagnostics).
    }
}
