namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S1 — Occupancy. Classroom doluluk oranı 60-85% hedef aralığında olmalı.
/// PLAN 3 SCOPE: Diagnostic stub. ConstraintFlags["S1"]=false ise hiçbir şey yapmaz;
/// true ise de Plan 3'te penalty matematiği eklenmemiş — early return.
/// PLAN 4 SCOPE: Real penalty implementation (per-classroom occupancy ratio target with deviation cost).
/// </summary>
public sealed class OccupancyEncoder : IConstraintEncoder
{
    public string Code => "S1";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        // Plan 3 diagnostic stub. Plan 4 will compute occupancy deviation penalty.
        // No penalty added; no assumption literal (soft constraints aren't UNSAT-core diagnostics).
    }
}
