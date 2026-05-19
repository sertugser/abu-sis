using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// H16 — TheoryLabDaySplit soft mode (Plan 10 Phase 3).
/// Hard encoder threshold üstü total saatte teori ile lab'ın aynı güne düşmesini yasaklıyor;
/// soft modda aynı güne düşerse <see cref="SoftThresholds.H16Weight"/> penalty eklenir.
/// </summary>
/// <remarks>
/// <para>
/// Threshold parametre Plan 10 Task 9'da deprecated kabul edildi — soft mod section'ın
/// total saatinden bağımsız "aynı gün ihlali" sayar (basitleştirilmiş semantik). Hard encoder
/// halen threshold uygular; soft encoder threshold-agnostic çalışır.
/// </para>
/// <para>
/// Pattern: per (theory+lab) section, <c>theoryDay == labDay</c> reified BoolVar × weight
/// penalty. AddMaxEquality / excess gerekmez; tek BoolVar yeterli.
/// </para>
/// </remarks>
public sealed class TheoryLabDaySplitSoftEncoder : IConstraintEncoder
{
    public string Code => "H16";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Hard");
        if (!string.Equals(category, "Soft", StringComparison.OrdinalIgnoreCase))
            return;

        var weight = (long)ctx.Request.SoftThresholds.H16Weight;
        if (weight <= 0) return;

        if (ctx.Request.TimeSlots.Count == 0) return;

        foreach (var section in ctx.Request.Sections)
        {
            if (section.TheoryHours <= 0 || section.LabHours <= 0) continue;

            var theoryDay = ctx.Variables.GetOrCreateComponentDayVar(
                section.Id, ComponentType.Lecture, ctx.Request.TimeSlots);
            var labDay = ctx.Variables.GetOrCreateComponentDayVar(
                section.Id, ComponentType.Lab, ctx.Request.TimeSlots);

            // lit ⇔ (theoryDay == labDay)
            var lit = ctx.Model.NewBoolVar($"h16soft_sameDay_s{section.Id}");
            ctx.Model.Add(theoryDay == labDay).OnlyEnforceIf(lit);
            ctx.Model.Add(theoryDay != labDay).OnlyEnforceIf(lit.Not());

            ctx.Penalties.Add(LinearExpr.Term(lit, weight));
        }
    }
}
