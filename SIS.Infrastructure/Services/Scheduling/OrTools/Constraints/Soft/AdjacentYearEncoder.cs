using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S20 — Adjacent Year Conflict. Aynı programda ardışık sınıf yıllarının (örn. 2. ve 3. yıl)
/// section'ları aynı slot'ta çakışmasın — öğrenci alttan/üstten ders alma esnekliğini
/// kaybetmesin. H5 CurriculumConflict aynı program×year için hard çakışmayı önler;
/// S20 ardışık yılları **soft** olarak ayırır.
/// v1 <c>AdjacentYearConflictEvaluator.cs:39-58</c> portu.
/// </summary>
/// <remarks>
/// Penalty formülü: <see cref="SolverRequest.AdjacentYearSoftPairs"/> içindeki her pair
/// için lecture interval'ler reify edilir; overlap olduğunda
/// <c>S20PenaltyPerPair × S20Weight</c> penalty objektife eklenir.
/// <para>
/// Pair canonical form: SolverRequestBuilder cohort modunda
/// <c>(SectionA, SectionB)</c> her zaman <c>SectionA &lt; SectionB</c> normalize gelir;
/// encoder bu invariant'a güvenir, double-count olmaz.
/// </para>
/// <para>
/// Overlap reification: <c>lit ⇔ (startA &lt; endB) ∧ (startB &lt; endA)</c>. İki yardımcı
/// boolean (<c>aLessB</c>, <c>bLessA</c>) bidirectional reify edilir, sonra
/// <c>AddBoolAnd</c> + <c>AddBoolOr</c> ile <c>lit</c>'e bağlanır
/// (S4 CoEnrollment ve diğer overlap encoder'lar ile aynı pattern).
/// </para>
/// <para>
/// NOTE (Plan 9): Yalnızca <see cref="ComponentType.Lecture"/> interval'ı iterate edilir
/// (v1 evaluator <c>TimeBlocks</c>'lar üzerinden tüm component'leri görür ama Plan 8'de
/// section-level lecture interval esas). Lab interval ve multi-block lab gap deferred —
/// S2/S4/S5 ile aynı kapsam kararı.
/// </para>
/// <para>
/// NOTE (Plan 12 Faz 9 — weight authority): V2 <c>SoftThresholds.S20Weight</c>
/// (<c>Schedule.S20Weight</c> SystemConfig) ile V1 <c>ConstraintDefinition.Weight</c>
/// (DB) **iki ayrı admin UI yolu**. Birebir uyum için tek weight authority kararı
/// gerek (admin "S20 ağırlığı" değiştirdiğinde hangi yere yansısın?). **Plan 13'e
/// ertelendi** — mimari karar gerek (önerim: ConstraintDefinition.Weight tek otorite,
/// SolverRequestBuilder oradan okusun, SystemConfig.Schedule.S20Weight kalkar).
/// </para>
/// </remarks>
public sealed class AdjacentYearEncoder : IConstraintEncoder
{
    public string Code => "S20";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;
        var t = ctx.Request.SoftThresholds;
        if (t.S20Weight <= 0) return;
        if (ctx.Request.AdjacentYearSoftPairs.Count == 0) return;

        // Promote to long BEFORE multiplication to avoid int overflow on extreme tuning
        // (Task 11 polish lesson: defensive long-promote even when current defaults are small).
        var penaltyCoeff = (long)t.S20PenaltyPerPair * t.S20Weight;
        if (penaltyCoeff <= 0) return;

        foreach (var (sectionA, sectionB) in ctx.Request.AdjacentYearSoftPairs)
        {
            if (sectionA == sectionB) continue; // self-pair guard

            var lecA = ctx.Variables.GetLectureInterval(sectionA);
            if (lecA == null) continue;
            var lecB = ctx.Variables.GetLectureInterval(sectionB);
            if (lecB == null) continue;

            // Reify overlap: lit ⇔ (startA < endB) ∧ (startB < endA)
            var aLessB = ctx.Model.NewBoolVar($"s20_aLessB_s{sectionA}_s{sectionB}");
            ctx.Model.Add(lecA.StartExpr() < lecB.EndExpr()).OnlyEnforceIf(aLessB);
            ctx.Model.Add(lecA.StartExpr() >= lecB.EndExpr()).OnlyEnforceIf(aLessB.Not());

            var bLessA = ctx.Model.NewBoolVar($"s20_bLessA_s{sectionA}_s{sectionB}");
            ctx.Model.Add(lecB.StartExpr() < lecA.EndExpr()).OnlyEnforceIf(bLessA);
            ctx.Model.Add(lecB.StartExpr() >= lecA.EndExpr()).OnlyEnforceIf(bLessA.Not());

            // lit ⇔ aLessB ∧ bLessA
            var lit = ctx.Model.NewBoolVar($"s20_overlap_s{sectionA}_s{sectionB}");
            ctx.Model.AddBoolAnd(new ILiteral[] { aLessB, bLessA }).OnlyEnforceIf(lit);
            ctx.Model.AddBoolOr(new ILiteral[] { aLessB.Not(), bLessA.Not() })
                .OnlyEnforceIf(lit.Not());

            ctx.Penalties.Add(LinearExpr.Term(lit, penaltyCoeff));
        }
    }
}
