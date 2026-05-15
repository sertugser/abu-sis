using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S8 — InstructorDaySpread. Hocanın derslerinin kaç farklı güne dağıldığı (usedDays);
/// minimum mümkün gün sayısı <c>ceil(totalHours / S2MaxDailyHours)</c>. usedDays bu
/// min'i aşarsa <c>(usedDays - minPossibleDays) × S8Weight</c> penalty objektif
/// fonksiyona eklenir. v1 <c>InstructorDaySpreadEvaluator.cs:25-39</c> portu.
/// </summary>
/// <remarks>
/// Reified literal pattern: her (instructor, day) çifti için
/// <c>dayUsed = OR(over sections: dayVar == day)</c> reify edilir, sonra
/// <c>usedDays = sum(dayUsed)</c> ve <c>excess = max(0, usedDays - minPossibleDays)</c>.
/// Bidirectional reification:
/// <list type="bullet">
///   <item><c>OR(perSectionDayLits) → dayUsed</c>: <c>AddBoolOr(...).OnlyEnforceIf(dayUsed)</c></item>
///   <item><c>each lit → dayUsed</c>: <c>AddImplication(lit, dayUsed)</c></item>
/// </list>
/// İkisi birlikte <c>dayUsed ⇔ OR(lits)</c> denkliğini tam kurar.
/// <para>
/// NOTE (Plan 9): Şu an yalnızca <see cref="ComponentType.Lecture"/> day var iterate edilir.
/// Multi-block lab'lar (Plan 7b) — her lab block'unun kendi day var'ı var, ancak bu encoder
/// yalnızca section'ın ana (lecture) component day var'ını okur. Lab-only section'lar
/// (TheoryHours=0) day var alınamadığı için <c>S2MaxDailyHours</c> üzerinden
/// totalHours hesabına dahil olur ama gün dağılımı sayılmaz; Plan 9'a deferred.
/// </para>
/// <para>
/// NOTE (v1 unit divergence): v1 evaluator gerçek clock-hour
/// (<c>Σ EndTime − StartTime</c>) kullanırken bu encoder credit-hour
/// (<c>section.TheoryHours + section.LabHours</c>) kullanıyor (DailyLoadSoftEncoder ile
/// aynı semantik). Plan 12 "birebir tutsun" kararı sonrası clock-hour migration
/// **Plan 13'e ertelendi** — CP-SAT block-level model genişletmesi gerek (her block
/// için duration int aggregation). Production cap (S2MaxDailyHours=6) için credit-hour
/// ile clock-hour arasındaki fark genellikle ≤1 saat (50dk slot grid'inde 6 credit
/// ≈ 5 clock); admin tune edebilir.
/// </para>
/// </remarks>
public sealed class InstructorDaySpreadEncoder : IConstraintEncoder
{
    public string Code => "S8";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;
        var t = ctx.Request.SoftThresholds;
        if (t.S8Weight <= 0 || t.S2MaxDailyHours <= 0) return;

        // Group sections by InstructorId (skip null instructors).
        // TheoryHours > 0 filter: lab-only sections have no Lecture interval, and
        // GetOrCreateComponentDayVar(Lecture, ...) would throw — match S2's filter exactly.
        var sectionsByInstructor = ctx.Request.Sections
            .Where(s => s.InstructorId.HasValue && s.TheoryHours > 0)
            .GroupBy(s => s.InstructorId!.Value)
            .ToList();

        if (sectionsByInstructor.Count == 0) return;

        foreach (var grp in sectionsByInstructor)
        {
            var totalHours = grp.Sum(s => s.TheoryHours + s.LabHours);
            if (totalHours <= 0) continue;

            // Integer ceiling: (totalHours + cap - 1) / cap; floor at 1.
            var minPossibleDays = Math.Max(
                1,
                (int)Math.Ceiling((double)totalHours / t.S2MaxDailyHours));

            // dayUsed[d] = OR over sections of "lecture is on day d" (bidirectional reify).
            var dayUsedLits = new List<BoolVar>();
            for (int day = 0; day < 7; day++)
            {
                var perSectionDayLits = new List<ILiteral>();
                foreach (var section in grp)
                {
                    var dayVar = ctx.Variables.GetOrCreateComponentDayVar(
                        section.Id, ComponentType.Lecture, ctx.Request.TimeSlots);

                    // reified: lit ⇔ (dayVar == day)
                    var lit = ctx.Model.NewBoolVar(
                        $"s8_i{grp.Key}_s{section.Id}_d{day}");
                    ctx.Model.Add(dayVar == day).OnlyEnforceIf(lit);
                    ctx.Model.Add(dayVar != day).OnlyEnforceIf(lit.Not());
                    perSectionDayLits.Add(lit);
                }

                var dayUsed = ctx.Model.NewBoolVar($"s8_i{grp.Key}_d{day}_used");
                // dayUsed → OR(perSectionDayLits)
                ctx.Model.AddBoolOr(perSectionDayLits.ToArray()).OnlyEnforceIf(dayUsed);
                // each lit → dayUsed (closes the equivalence)
                foreach (var lit in perSectionDayLits)
                    ctx.Model.AddImplication(lit, dayUsed);

                dayUsedLits.Add(dayUsed);
            }

            if (dayUsedLits.Count == 0) continue;

            var usedDays = LinearExpr.Sum(dayUsedLits.ToArray());
            // excess upper bound: 7 (max distinct weekdays), floor 0.
            var excess = ctx.Model.NewIntVar(0, 7, $"s8_excess_i{grp.Key}");
            ctx.Model.AddMaxEquality(excess, new[]
            {
                usedDays - LinearExpr.Constant(minPossibleDays),
                LinearExpr.Constant(0)
            });
            // (long) cast: overflow safety for large weight × large excess.
            ctx.Penalties.Add(excess * (long)t.S8Weight);
        }
    }
}
