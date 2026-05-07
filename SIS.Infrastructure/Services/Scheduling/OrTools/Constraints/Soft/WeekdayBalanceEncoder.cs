namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S12 — Weekday Balance. Section'lar haftanın günlerine dengeli dağılsın — Pazartesi
/// dolu Cuma boş olmasın. Hem öğrenci hem hoca hem derslik kullanımı bu kısıttan etkilenir.
/// PLAN 3 SCOPE: Diagnostic stub. ConstraintFlags["S12"]=false ise hiçbir şey yapmaz;
/// true ise de Plan 3'te penalty matematiği eklenmemiş — early return.
/// PLAN 4 SCOPE: Real penalty implementation (per-day section count vs target average,
/// L1 deviation cost).
/// </summary>
public sealed class WeekdayBalanceEncoder : IConstraintEncoder
{
    public string Code => "S12";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        // Plan 3 diagnostic stub. Plan 4 will compute weekday-balance deviation penalty.
        // No penalty added; no assumption literal (soft constraints aren't UNSAT-core diagnostics).
    }
}
