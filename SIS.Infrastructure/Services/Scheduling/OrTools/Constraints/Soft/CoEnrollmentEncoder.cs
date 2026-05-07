namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S4 — Co-Enrollment. Aynı öğrencilerin sıkça birlikte aldığı iki dersin schedule'ı
/// birbirine yakın (aynı gün ya da bitişik slot) olsun — kampüsler arası mekik azalsın.
/// PLAN 3 SCOPE: Diagnostic stub. ConstraintFlags["S4"]=false ise hiçbir şey yapmaz;
/// true ise de Plan 3'te penalty matematiği eklenmemiş — early return.
/// PLAN 4 SCOPE: Real penalty implementation (per-(courseA, courseB) co-enrollment matrix
/// driving distance/time-gap penalty between section schedules).
/// </summary>
public sealed class CoEnrollmentEncoder : IConstraintEncoder
{
    public string Code => "S4";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        // Plan 3 diagnostic stub. Plan 4 will compute co-enrollment proximity penalty.
        // No penalty added; no assumption literal (soft constraints aren't UNSAT-core diagnostics).
    }
}
