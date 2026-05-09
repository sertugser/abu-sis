using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S12 — Weekday Balance. Section'lar haftanın günlerine dengeli dağılsın — Pazartesi
/// dolu Cuma boş olmasın. Her gün için section sayısı (dayCount) reified literal toplamı
/// olarak modellenir; <c>diff = max(dayCount) - min(dayCount)</c> alınıp
/// <c>diff × S12Weight</c> penalty objektif fonksiyona eklenir. v1
/// <c>WeekdayBalanceEvaluator.cs:31-40</c> portu.
/// </summary>
/// <remarks>
/// Reified literal pattern: her (section, day) çifti için
/// <c>lit ⇔ (lectureDayVar == day)</c> reify edilir, sonra
/// <c>dayCount[d] = sum(lit_for_each_section)</c>. Tüm dayCount'lar üzerinden
/// <see cref="CpModel.AddMaxEquality"/> ve <see cref="CpModel.AddMinEquality"/> ile
/// max/min IntVar'ları kurulur, fark penalty olarak eklenir.
/// <para>
/// NOTE (Plan 9): Şu an yalnızca <see cref="ComponentType.Lecture"/> day var iterate
/// edilir. Multi-block lab'lar (Plan 7b) — her lab block'unun kendi day var'ı var, ancak
/// bu encoder lab interval'larını saymıyor. Lab-only section'lar (TheoryHours=0)
/// <c>GetOrCreateComponentDayVar(Lecture, ...)</c> throw ettiği için filtre dışı
/// bırakılır (S2/S8 paritesi). Multi-block lab + 7-day loop optimizasyonu Plan 9'a
/// deferred.
/// </para>
/// <para>
/// NOTE (v1 divergence — Plan 8 simplification): v1 evaluator
/// <c>(max - min) > avg × S12MaxImbalancePercent / 100</c> threshold'u sonrası penalty
/// uyguluyordu. Plan 8 CP-SAT için reified threshold gereksiz karmaşıklık olarak
/// değerlendirilip kaldırıldı; artık always-on penalty <c>(max - min) × S12Weight</c>.
/// Threshold equivalent solver'ın doğal denge eğilimi ile zaten ortaya çıkıyor (plan
/// kararı, Task 10 lines 2059-2060). <see cref="SoftThresholds.S12MaxImbalancePercent"/>
/// alanı encoder tarafından okunmuyor — yalnızca v1 evaluator paritesi için tutuluyor.
/// </para>
/// </remarks>
public sealed class WeekdayBalanceEncoder : IConstraintEncoder
{
    public string Code => "S12";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;
        var t = ctx.Request.SoftThresholds;
        if (t.S12Weight <= 0) return;
        if (ctx.Request.Sections.Count == 0) return;

        // TheoryHours > 0 filter: lab-only sections have no Lecture interval, and
        // GetOrCreateComponentDayVar(Lecture, ...) would throw — match S2/S8 filter.
        var sections = ctx.Request.Sections.Where(s => s.TheoryHours > 0).ToList();
        if (sections.Count == 0) return;

        var maxSections = sections.Count;

        // dayCount[d] = sum over sections of "section.lecture starts on day d" (reified).
        var dayCountVars = new IntVar[7];
        for (int day = 0; day < 7; day++)
        {
            var perSectionDayLits = new List<BoolVar>();
            foreach (var section in sections)
            {
                var dayVar = ctx.Variables.GetOrCreateComponentDayVar(
                    section.Id, ComponentType.Lecture, ctx.Request.TimeSlots);

                // reified: lit ⇔ (dayVar == day)
                var lit = ctx.Model.NewBoolVar($"s12_s{section.Id}_d{day}");
                ctx.Model.Add(dayVar == day).OnlyEnforceIf(lit);
                ctx.Model.Add(dayVar != day).OnlyEnforceIf(lit.Not());
                perSectionDayLits.Add(lit);
            }

            var count = ctx.Model.NewIntVar(0, maxSections, $"s12_d{day}_count");
            ctx.Model.Add(count == LinearExpr.Sum(perSectionDayLits.ToArray()));
            dayCountVars[day] = count;
        }

        var maxV = ctx.Model.NewIntVar(0, maxSections, "s12_max");
        var minV = ctx.Model.NewIntVar(0, maxSections, "s12_min");
        ctx.Model.AddMaxEquality(maxV, dayCountVars);
        ctx.Model.AddMinEquality(minV, dayCountVars);

        var diff = ctx.Model.NewIntVar(0, maxSections, "s12_diff");
        ctx.Model.Add(diff == maxV - minV);

        // (long) cast: overflow safety for large weight × large diff.
        ctx.Penalties.Add(diff * (long)t.S12Weight);
    }
}
