using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// H-WL — WeeklyLoad soft mode (Plan 10 Phase 3).
/// Hard encoder hocanın haftalık toplam ders saatini <c>maxHours</c> ile sınırlandırıyor;
/// soft modda fazlalık (excess) <see cref="SoftThresholds.HWLWeight"/> ile çarpılıp
/// objektif fonksiyonun penalty kısmına eklenir.
/// </summary>
/// <remarks>
/// Per instructor: <c>sum(present[s] × hours[s]) - maxHours</c> excess'i
/// <c>AddMaxEquality(excess, sum - cap, 0)</c> ile reify edilir, weight çarpılır.
/// DailyLoadSoft pattern'inin haftalık variantı.
/// </remarks>
public sealed class WeeklyLoadSoftEncoder : IConstraintEncoder
{
    public string Code => "H-WL";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Hard");
        if (!string.Equals(category, "Soft", StringComparison.OrdinalIgnoreCase))
            return;

        var weight = (long)ctx.Request.SoftThresholds.HWLWeight;
        if (weight <= 0) return;

        var byInstructor = ctx.Request.Sections
            .Where(s => s.InstructorId.HasValue)
            .GroupBy(s => s.InstructorId!.Value);

        foreach (var group in byInstructor)
        {
            var sectionList = group.ToList();

            // Same resolution rule as hard encoder.
            var overrideMax = sectionList.Max(s => s.InstructorMaxWeeklyHours);
            var maxHours = overrideMax > 0 ? overrideMax : ctx.Request.DefaultInstructorMaxWeeklyHours;
            if (maxHours <= 0) continue;

            var terms = new List<LinearExpr>();
            int maxPossible = 0;
            foreach (var section in sectionList)
            {
                if (ctx.Variables.GetSectionPresent(section.Id) is not IntVar present)
                    continue;
                var hours = section.TheoryHours + section.LabHours;
                if (hours <= 0) continue;
                terms.Add(LinearExpr.Term(present, hours));
                maxPossible += hours;
            }
            if (terms.Count == 0) continue;

            var totalHours = LinearExpr.Sum(terms.ToArray());
            var excess = ctx.Model.NewIntVar(0, maxPossible,
                $"hwlsoft_excess_i{group.Key}");
            ctx.Model.AddMaxEquality(excess, new[]
            {
                totalHours - LinearExpr.Constant(maxHours),
                LinearExpr.Constant(0)
            });
            ctx.Penalties.Add(excess * weight);
        }
    }
}
