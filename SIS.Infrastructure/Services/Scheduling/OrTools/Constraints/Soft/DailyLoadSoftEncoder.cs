using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S2 — DailyLoadSoft. Per-(instructor, weekday) ders saatleri toplamı
/// <see cref="SoftThresholds.S2MaxDailyHours"/> üzerine çıkarsa
/// (excess) × <see cref="SoftThresholds.S2Weight"/> penalty objektif fonksiyona eklenir.
/// v1 <c>DailyLoadSoftEvaluator.cs:30-39</c> portu.
/// </summary>
/// <remarks>
/// Reified literal pattern: section'ın lecture day var'ı belirli güne eşit mi? sorusu
/// boolean'a dönüştürülür, bu boolean'ın <c>section.TheoryHours + section.LabHours</c>
/// ile çarpımı günlük yük toplamına katılır. <c>excess = max(0, dailyHours - cap)</c>
/// <c>AddMaxEquality</c> ile reify edilir, sonuç <c>S2Weight</c> ile çarpılıp
/// <c>ctx.Penalties</c>'e eklenir.
/// <para>
/// NOTE (Plan 9): Şu an yalnızca <see cref="ComponentType.Lecture"/> day var iterate edilir.
/// Multi-block lab'ların (Plan 7b) per-block daily-load aggregation'ı Plan 9'a deferred —
/// her lab block'unun kendi day var'ı var, ancak bu encoder yalnızca section'ın ana
/// (lecture) component day var'ını okur. Lab-only section'lar (TheoryHours=0) day var
/// alınamadığı için silently skip olur.
/// </para>
/// <para>
/// NOTE (v1 unit divergence — Plan 9 Issue 5 — CLOSED): v1 evaluator gerçek clock-hour
/// (<c>slot.EndTime - slot.StartTime</c>) topluyor; bu encoder credit-hour
/// (<c>section.TheoryHours + section.LabHours</c>) kullanıyor. Plan 9'da kararlaştırıldı:
/// real clock-hour implementasyonu model karmaşıklığını artırır (slot duration × slot count
/// per-instructor reified literal). Yerine default cap 6 → 7 credit-hour'a yükseltildi
/// (50dk slot grid'inde 7 credit ≈ 5.8 clock ≈ v1 default 6 clock). Admin
/// <c>Schedule.S2.MaxDailyHours</c>'u real DB tune sonrası override edebilir; DB seed
/// migration üzerinden 6 olarak kalıyor (kod default 7 yalnız config eksik fresh test
/// senaryolarında devreye girer).
/// </para>
/// </remarks>
public sealed class DailyLoadSoftEncoder : IConstraintEncoder
{
    public string Code => "S2";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;
        var t = ctx.Request.SoftThresholds;
        if (t.S2Weight <= 0) return;

        // Group sections by InstructorId (skip null instructors).
        var sectionsByInstructor = ctx.Request.Sections
            .Where(s => s.InstructorId.HasValue && s.TheoryHours > 0)
            .GroupBy(s => s.InstructorId!.Value)
            .ToList();

        if (sectionsByInstructor.Count == 0) return;

        foreach (var instructorGroup in sectionsByInstructor)
        {
            // For each weekday (0..6), aggregate hours-of-sections-starting-that-day.
            for (int day = 0; day < 7; day++)
            {
                var dailyTerms = new List<LinearExpr>();
                foreach (var section in instructorGroup)
                {
                    var dayVar = ctx.Variables.GetOrCreateComponentDayVar(
                        section.Id, ComponentType.Lecture, ctx.Request.TimeSlots);

                    // reified: lit ⇔ (dayVar == day)
                    var lit = ctx.Model.NewBoolVar(
                        $"s2_i{instructorGroup.Key}_s{section.Id}_d{day}");
                    ctx.Model.Add(dayVar == day).OnlyEnforceIf(lit);
                    ctx.Model.Add(dayVar != day).OnlyEnforceIf(lit.Not());

                    var hours = section.TheoryHours + section.LabHours;
                    if (hours > 0)
                        dailyTerms.Add(LinearExpr.Term(lit, hours));
                }

                if (dailyTerms.Count == 0) continue;

                var dailyHours = LinearExpr.Sum(dailyTerms.ToArray());
                var maxPossible = instructorGroup.Sum(s => s.TheoryHours + s.LabHours);
                var excess = ctx.Model.NewIntVar(
                    0, maxPossible, $"s2_excess_i{instructorGroup.Key}_d{day}");
                ctx.Model.AddMaxEquality(excess, new[]
                {
                    dailyHours - LinearExpr.Constant(t.S2MaxDailyHours),
                    LinearExpr.Constant(0)
                });
                // (long) cast: overflow safety for large weight × large excess.
                ctx.Penalties.Add(excess * (long)t.S2Weight);
            }
        }
    }
}
