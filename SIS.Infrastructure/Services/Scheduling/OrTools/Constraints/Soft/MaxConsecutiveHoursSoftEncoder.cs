using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// H15 — MaxConsecutiveHours soft mode (Plan 10 Phase 3, simplified).
/// Hard encoder slot-coverage matrix + sliding-window pattern ile aralıksız blok cap'ini
/// uyguluyor; soft modda ekstra saat başına penalty hedeflenir. Tam sliding-window reified
/// matrix soft mode için maliyetli (sections × totalSlots × instructors reified BoolVar);
/// bu encoder daha basit bir günlük-toplam yaklaşımı kullanır:
/// <c>excess = max(0, dailyHours - cap)</c> her (instructor, day) için DailyLoadSoft pattern'iyle.
/// </summary>
/// <remarks>
/// <para>
/// Bu basitleştirme DailyLoadSoft (S2) ile semantik olarak yakın çalışır ama farklı cap
/// değeri ile (<c>maxConsecutiveHours</c> Schedule.MaxConsecutiveHoursPerDay'den, default 4).
/// Gerçek "aralıksız" semantiği (gap-detection) Plan 10+'da hard pattern'in soft variant'ı
/// olarak portlanabilir; şu anki implementation günlük yük üst sınırı olarak çalışır.
/// </para>
/// <para>
/// Cap default=4 (hard encoder constructor default). SolverRequest üzerinden değişiklik
/// için yeni field gerekmiyor — SoftThresholds.H15Weight admin tunable.
/// </para>
/// </remarks>
public sealed class MaxConsecutiveHoursSoftEncoder : IConstraintEncoder
{
    public string Code => "H15";
    public bool IsHard => false;

    private readonly int _maxConsecutiveHours;

    public MaxConsecutiveHoursSoftEncoder(int maxConsecutiveHours = 4)
    {
        if (maxConsecutiveHours <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConsecutiveHours),
                maxConsecutiveHours, "maxConsecutiveHours must be positive.");
        _maxConsecutiveHours = maxConsecutiveHours;
    }

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Hard");
        if (!string.Equals(category, "Soft", StringComparison.OrdinalIgnoreCase))
            return;

        var weight = (long)ctx.Request.SoftThresholds.H15Weight;
        if (weight <= 0) return;

        if (ctx.Request.TimeSlots.Count == 0) return;

        var byInstructor = ctx.Request.Sections
            .Where(s => s.InstructorId.HasValue && s.TheoryHours > 0)
            .GroupBy(s => s.InstructorId!.Value)
            .ToList();
        if (byInstructor.Count == 0) return;

        foreach (var group in byInstructor)
        {
            for (int day = 0; day < 7; day++)
            {
                var dailyTerms = new List<LinearExpr>();
                foreach (var section in group)
                {
                    var dayVar = ctx.Variables.GetOrCreateComponentDayVar(
                        section.Id, ComponentType.Lecture, ctx.Request.TimeSlots);

                    var lit = ctx.Model.NewBoolVar(
                        $"h15soft_i{group.Key}_s{section.Id}_d{day}");
                    ctx.Model.Add(dayVar == day).OnlyEnforceIf(lit);
                    ctx.Model.Add(dayVar != day).OnlyEnforceIf(lit.Not());

                    var hours = section.TheoryHours + section.LabHours;
                    if (hours > 0)
                        dailyTerms.Add(LinearExpr.Term(lit, hours));
                }

                if (dailyTerms.Count == 0) continue;

                var dailyHours = LinearExpr.Sum(dailyTerms.ToArray());
                var maxPossible = group.Sum(s => s.TheoryHours + s.LabHours);
                var excess = ctx.Model.NewIntVar(
                    0, maxPossible, $"h15soft_excess_i{group.Key}_d{day}");
                ctx.Model.AddMaxEquality(excess, new[]
                {
                    dailyHours - LinearExpr.Constant(_maxConsecutiveHours),
                    LinearExpr.Constant(0)
                });
                ctx.Penalties.Add(excess * weight);
            }
        }
    }
}
