using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S4 — Co-Enrollment. İki dersin (örn. ENG101 ve MATH101) sıkça aynı öğrenciler
/// tarafından birlikte alındığı durumlarda section'ları zaman olarak çakışmasın.
/// v1 <c>CoEnrollmentEvaluator.cs:28-44</c> portu.
/// </summary>
/// <remarks>
/// Penalty formülü: per-(courseA section × courseB section) overlap reify edilir;
/// overlap olduğunda <c>Max(1, StudentCount × S4Weight / 2)</c> penalty objektife eklenir.
/// v1 formülü <c>StudentCount × Weight × 0.5</c> floating-point; integer port'ta
/// <c>StudentCount × Weight / 2</c> ile bölme — even <c>StudentCount</c> için aynı,
/// odd için truncation (örn. 31 öğrenci × 1 / 2 = 15 vs v1'de 15.5). Min 1 floor
/// koruması "sıfır penalty" edge'ini önler (<c>StudentCount × Weight</c> = 1 olduğunda).
/// <para>
/// Pair canonical form: <see cref="SolverRequest.CoEnrollmentPairs"/> her zaman
/// <c>CourseId1 &lt; CourseId2</c> normalize gelir; encoder bu invariant'a güvenir,
/// double-count olmaz.
/// </para>
/// <para>
/// Overlap reification: <c>lit ⇔ (startA &lt; endB) ∧ (startB &lt; endA)</c>. İki yardımcı
/// boolean (<c>aLessB</c>, <c>bLessA</c>) bidirectional reify edilir, sonra
/// <c>AddBoolAnd</c> + <c>AddBoolOr</c> ile <c>lit</c>'e bağlanır
/// (v1 paritesi: <see cref="SchedulingHelpers.GetOverlapSlots"/> implicit interval-overlap).
/// </para>
/// <para>
/// NOTE (Plan 9): Yalnızca <see cref="ComponentType.Lecture"/> interval'ı iterate edilir
/// (v1 evaluator <c>TimeBlocks</c>'lar üzerinden tüm component'leri görür ama Plan 8'de
/// section-level lecture interval esas). Lab interval ve multi-block lab gap deferred —
/// S5/S2 ile aynı kapsam kararı.
/// </para>
/// </remarks>
public sealed class CoEnrollmentEncoder : IConstraintEncoder
{
    public string Code => "S4";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;
        var t = ctx.Request.SoftThresholds;
        if (t.S4Weight <= 0) return;
        if (ctx.Request.CoEnrollmentPairs.Count == 0) return;

        // Group sections by CourseId for fast pair lookup.
        var sectionsByCourse = ctx.Request.Sections
            .Where(s => s.TheoryHours > 0)
            .GroupBy(s => s.CourseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var pair in ctx.Request.CoEnrollmentPairs)
        {
            if (!sectionsByCourse.TryGetValue(pair.CourseId1, out var sectionsA)) continue;
            if (!sectionsByCourse.TryGetValue(pair.CourseId2, out var sectionsB)) continue;

            // v1 formula: pair.StudentCount × Weight × 0.5 → integer port: Max(1, n × w / 2).
            // Min-1 floor protects the (n × w) == 1 edge from collapsing to zero penalty.
            // Promote to long BEFORE multiplication to avoid int overflow on extreme tuning
            // (e.g. StudentCount=5000 × Weight=500 = 2.5B > int.MaxValue).
            var penaltyCoeff = Math.Max(1L, (long)pair.StudentCount * t.S4Weight / 2);

            foreach (var a in sectionsA)
            {
                var lecA = ctx.Variables.GetLectureInterval(a.Id);
                if (lecA == null) continue;

                foreach (var b in sectionsB)
                {
                    if (a.Id == b.Id) continue; // self-pair guard

                    var lecB = ctx.Variables.GetLectureInterval(b.Id);
                    if (lecB == null) continue;

                    // Reify overlap: lit ⇔ (startA < endB) ∧ (startB < endA)
                    var aLessB = ctx.Model.NewBoolVar($"s4_aLessB_s{a.Id}_s{b.Id}");
                    ctx.Model.Add(lecA.StartExpr() < lecB.EndExpr()).OnlyEnforceIf(aLessB);
                    ctx.Model.Add(lecA.StartExpr() >= lecB.EndExpr()).OnlyEnforceIf(aLessB.Not());

                    var bLessA = ctx.Model.NewBoolVar($"s4_bLessA_s{a.Id}_s{b.Id}");
                    ctx.Model.Add(lecB.StartExpr() < lecA.EndExpr()).OnlyEnforceIf(bLessA);
                    ctx.Model.Add(lecB.StartExpr() >= lecA.EndExpr()).OnlyEnforceIf(bLessA.Not());

                    // lit ⇔ aLessB ∧ bLessA
                    var lit = ctx.Model.NewBoolVar($"s4_overlap_s{a.Id}_s{b.Id}");
                    ctx.Model.AddBoolAnd(new ILiteral[] { aLessB, bLessA }).OnlyEnforceIf(lit);
                    ctx.Model.AddBoolOr(new ILiteral[] { aLessB.Not(), bLessA.Not() })
                        .OnlyEnforceIf(lit.Not());

                    // (long) cast: overflow safety for large StudentCount × Weight values.
                    ctx.Penalties.Add(LinearExpr.Term(lit, penaltyCoeff));
                }
            }
        }
    }
}
