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
/// V1 alignment (Plan 12 Faz 9 — 2026-05-15, kullanıcı "birebir tutsun" kararı):
/// V1 evaluator <c>(max − min) > avg × S12MaxImbalancePercent / 100</c> threshold guard
/// uyguluyor. Plan 8'de "gereksiz karmaşıklık" diye kaldırılmıştı; Plan 12'de geri
/// eklendi — küçük dengesizliklere optimize'ı zorlamamak için. Reified linear
/// constraint: <c>triggered ⇔ diff × 700 > sumOfDays × percent</c> (avg=sumOfDays/7
/// çapraz çarpımı, integer-safe). triggered=true ise penalty <c>diff × S12Weight</c>,
/// değilse 0.
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

        // NOTE (Plan 9): All 7 days iterated unconditionally. On weekday-only grids
        // (Mon-Fri), Sat/Sun count IntVars are trivially 0, so min=0 always → diff=max
        // → encoder produces persistent penalty even on perfectly balanced Mon-Fri grids.
        // v1 evaluator has the same structural bias. Plan 9 fix: filter dayCountVars to
        // active grid days (TimeSlots.DayOfWeek distinct) before Min/Max equality.
        var maxV = ctx.Model.NewIntVar(0, maxSections, "s12_max");
        var minV = ctx.Model.NewIntVar(0, maxSections, "s12_min");
        ctx.Model.AddMaxEquality(maxV, dayCountVars);
        ctx.Model.AddMinEquality(minV, dayCountVars);

        var diff = ctx.Model.NewIntVar(0, maxSections, "s12_diff");
        ctx.Model.Add(diff == maxV - minV);

        // Plan 12 Faz 9 V1 alignment — threshold guard.
        // V1: diff > avg × percent / 100 = (sumOfDays/7) × percent / 100
        // Cross-multiply (integer-safe): diff × 700 > sumOfDays × percent
        var percent = t.S12MaxImbalancePercent > 0 ? t.S12MaxImbalancePercent : 20;
        var sumOfDays = ctx.Model.NewIntVar(0, maxSections * 7, "s12_sumOfDays");
        ctx.Model.Add(sumOfDays == LinearExpr.Sum(dayCountVars));

        var triggered = ctx.Model.NewBoolVar("s12_triggered");
        ctx.Model.Add(diff * 700 > sumOfDays * percent).OnlyEnforceIf(triggered);
        ctx.Model.Add(diff * 700 <= sumOfDays * percent).OnlyEnforceIf(triggered.Not());

        // effectivePenalty = triggered ? (diff × weight) : 0
        var maxEffective = (long)maxSections * t.S12Weight;
        var effective = ctx.Model.NewIntVar(0, maxEffective, "s12_effective");
        ctx.Model.Add(effective == diff * (long)t.S12Weight).OnlyEnforceIf(triggered);
        ctx.Model.Add(effective == 0).OnlyEnforceIf(triggered.Not());
        ctx.Penalties.Add(effective);
    }
}
