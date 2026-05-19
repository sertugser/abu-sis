using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// H8 — SameCourseSection soft mode (Plan 10 Phase 3).
/// Hard encoder aynı dersin section'larını <c>AddNoOverlap</c> ile ayırıyor (öğrenci hangi
/// şubeyi seçerse seçsin saatler farklı olsun). Soft modda overlap'a izin verilir, her
/// overlap çifti için <see cref="SoftThresholds.H8Weight"/> penalty eklenir
/// ("admin: aynı ders başka şubeleriyle çakışsa da pratik gerekçeyle uygundur" tercihi).
/// </summary>
/// <remarks>
/// Hem lecture hem lab component'i için per-pair reified overlap × weight. AdjacentYearEncoder
/// ile aynı pattern.
/// </remarks>
public sealed class SameCourseSectionSoftEncoder : IConstraintEncoder
{
    public string Code => "H8";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Hard");
        if (!string.Equals(category, "Soft", StringComparison.OrdinalIgnoreCase))
            return;

        var weight = (long)ctx.Request.SoftThresholds.H8Weight;
        if (weight <= 0) return;

        var byCourse = ctx.Request.Sections.GroupBy(s => s.CourseId);

        foreach (var courseGroup in byCourse)
        {
            // Lecture pairs
            var lectureSections = courseGroup.Where(s => s.TheoryHours > 0).ToList();
            EncodeGroupPenalty(ctx, lectureSections, useLecture: true, weight, "lec");

            // Lab pairs
            var labSections = courseGroup.Where(s => s.LabHours > 0).ToList();
            EncodeGroupPenalty(ctx, labSections, useLecture: false, weight, "lab");
        }
    }

    private static void EncodeGroupPenalty(
        EncoderContext ctx,
        IReadOnlyList<SectionInput> sections,
        bool useLecture,
        long weight,
        string tag)
    {
        if (sections.Count < 2) return;

        for (int i = 0; i < sections.Count; i++)
        {
            for (int j = i + 1; j < sections.Count; j++)
            {
                var ivA = useLecture
                    ? ctx.Variables.GetLectureInterval(sections[i].Id)
                    : ctx.Variables.GetLabInterval(sections[i].Id);
                var ivB = useLecture
                    ? ctx.Variables.GetLectureInterval(sections[j].Id)
                    : ctx.Variables.GetLabInterval(sections[j].Id);
                if (ivA == null || ivB == null) continue;

                var sa = sections[i].Id;
                var sb = sections[j].Id;

                var aLessB = ctx.Model.NewBoolVar($"h8soft_{tag}_aLessB_s{sa}_s{sb}");
                ctx.Model.Add(ivA.StartExpr() < ivB.EndExpr()).OnlyEnforceIf(aLessB);
                ctx.Model.Add(ivA.StartExpr() >= ivB.EndExpr()).OnlyEnforceIf(aLessB.Not());

                var bLessA = ctx.Model.NewBoolVar($"h8soft_{tag}_bLessA_s{sa}_s{sb}");
                ctx.Model.Add(ivB.StartExpr() < ivA.EndExpr()).OnlyEnforceIf(bLessA);
                ctx.Model.Add(ivB.StartExpr() >= ivA.EndExpr()).OnlyEnforceIf(bLessA.Not());

                var lit = ctx.Model.NewBoolVar($"h8soft_{tag}_overlap_s{sa}_s{sb}");
                ctx.Model.AddBoolAnd(new ILiteral[] { aLessB, bLessA }).OnlyEnforceIf(lit);
                ctx.Model.AddBoolOr(new ILiteral[] { aLessB.Not(), bLessA.Not() }).OnlyEnforceIf(lit.Not());

                ctx.Penalties.Add(LinearExpr.Term(lit, weight));
            }
        }
    }
}
