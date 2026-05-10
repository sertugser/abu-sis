using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// H5 — CurriculumConflict soft mode (Plan 10 Phase 3).
/// Hard encoder aynı (DepartmentId, Year) cohort'undaki farklı course'ların section'larını
/// <c>AddNoOverlap</c> ile ayırıyor; soft modda overlap'a izin verilir, her overlap çifti
/// için <see cref="SoftThresholds.H5Weight"/> penalty eklenir. AdjacentYearEncoder ile aynı
/// reified-overlap pattern'i kullanılır.
/// </summary>
/// <remarks>
/// <para>
/// Mode dispatch: SolverRequest.CurriculumConflictMode aynısı (curriculum/cohort/enrollment).
/// "enrollment" Plan 6.5 ertelendi — bu encoder'da da skip.
/// </para>
/// <para>
/// Pair canonical form: hard encoder'ın iterations'ı per group-of-different-courses içindeki
/// her iki section pair'i için reified overlap × weight. Cohort modunda
/// <see cref="SolverRequest.CohortConflictPairs"/> doğrudan iterate edilir
/// (SolverRequestBuilder normalize ediyor).
/// </para>
/// <para>
/// NOTE: Sadece lecture interval kullanılır (S20 ile aynı kapsam kararı; lab interval
/// ve multi-block lab gap parity Plan 11+'a deferred).
/// </para>
/// </remarks>
public sealed class CurriculumConflictSoftEncoder : IConstraintEncoder
{
    public string Code => "H5";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Hard");
        if (!string.Equals(category, "Soft", StringComparison.OrdinalIgnoreCase))
            return;

        var weight = (long)ctx.Request.SoftThresholds.H5Weight;
        if (weight <= 0) return;

        switch (ctx.Request.CurriculumConflictMode)
        {
            case "cohort":
                EncodeCohortMode(ctx, weight);
                break;
            case "enrollment":
                // Plan 6.5'e ertelendi
                break;
            default:
                EncodeCurriculumMode(ctx, weight);
                break;
        }
    }

    private static void EncodeCurriculumMode(EncoderContext ctx, long weight)
    {
        var groups = ctx.Request.Sections
            .Where(s => s.DepartmentId.HasValue && s.Year.HasValue)
            .GroupBy(s => (DeptId: s.DepartmentId!.Value, Year: s.Year!.Value));

        foreach (var group in groups)
        {
            var byCourse = group.GroupBy(s => s.CourseId).ToList();
            if (byCourse.Count < 2) continue;

            // Build all cross-course section pairs within the cohort.
            for (int i = 0; i < byCourse.Count; i++)
            {
                for (int j = i + 1; j < byCourse.Count; j++)
                {
                    foreach (var sectionA in byCourse[i])
                    {
                        foreach (var sectionB in byCourse[j])
                        {
                            AddPairOverlapPenalty(ctx, sectionA.Id, sectionB.Id, weight);
                        }
                    }
                }
            }
        }
    }

    private static void EncodeCohortMode(EncoderContext ctx, long weight)
    {
        if (ctx.Request.CohortConflictPairs.Count == 0) return;

        foreach (var (a, b) in ctx.Request.CohortConflictPairs)
        {
            if (a == b) continue;
            AddPairOverlapPenalty(ctx, a, b, weight);
        }
    }

    private static void AddPairOverlapPenalty(EncoderContext ctx, int sectionA, int sectionB, long weight)
    {
        var lecA = ctx.Variables.GetLectureInterval(sectionA);
        if (lecA == null) return;
        var lecB = ctx.Variables.GetLectureInterval(sectionB);
        if (lecB == null) return;

        var aLessB = ctx.Model.NewBoolVar($"h5soft_aLessB_s{sectionA}_s{sectionB}");
        ctx.Model.Add(lecA.StartExpr() < lecB.EndExpr()).OnlyEnforceIf(aLessB);
        ctx.Model.Add(lecA.StartExpr() >= lecB.EndExpr()).OnlyEnforceIf(aLessB.Not());

        var bLessA = ctx.Model.NewBoolVar($"h5soft_bLessA_s{sectionA}_s{sectionB}");
        ctx.Model.Add(lecB.StartExpr() < lecA.EndExpr()).OnlyEnforceIf(bLessA);
        ctx.Model.Add(lecB.StartExpr() >= lecA.EndExpr()).OnlyEnforceIf(bLessA.Not());

        var lit = ctx.Model.NewBoolVar($"h5soft_overlap_s{sectionA}_s{sectionB}");
        ctx.Model.AddBoolAnd(new ILiteral[] { aLessB, bLessA }).OnlyEnforceIf(lit);
        ctx.Model.AddBoolOr(new ILiteral[] { aLessB.Not(), bLessA.Not() }).OnlyEnforceIf(lit.Not());

        ctx.Penalties.Add(LinearExpr.Term(lit, weight));
    }
}
