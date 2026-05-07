namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S20 — Adjacent Year Conflict. Aynı programda ardışık sınıf yıllarının (örn. 2. ve 3. yıl)
/// section'ları aynı slot'ta çakışmasın — öğrenci alttan/üstten ders alma esnekliğini
/// kaybetmesin. H5 CurriculumConflict aynı program×year için hard çakışmayı önler;
/// S20 ardışık yılları **soft** olarak ayırır.
/// PLAN 3 SCOPE: Diagnostic stub. ConstraintFlags["S20"]=false ise hiçbir şey yapmaz;
/// true ise de Plan 3'te penalty matematiği eklenmemiş — early return.
/// PLAN 4 SCOPE: Real penalty implementation (per-(program, year, year+1) overlap-slot count
/// penalty).
/// </summary>
public sealed class AdjacentYearEncoder : IConstraintEncoder
{
    public string Code => "S20";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        // Plan 3 diagnostic stub. Plan 4 will compute adjacent-year overlap penalty.
        // No penalty added; no assumption literal (soft constraints aren't UNSAT-core diagnostics).
    }
}
