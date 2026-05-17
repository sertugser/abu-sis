using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S7 — Student Day-Internal Gaps. Aynı (DepartmentId, Year) grubundaki section çiftleri arasında
/// "aynı gün ∧ aralarında ≥ S7MaxGapSlots boş slot" durumu için per-pair sabit
/// <see cref="SoftThresholds.S7Weight"/> penalty objektife eklenir.
/// v1 <c>StudentGapEvaluator.cs:42-52</c> portu, simplified.
/// </summary>
/// <remarks>
/// <para>
/// V1 alignment (Plan 13 başlatma 2026-05-15): V1 evaluator
/// <c>(gap − cap + 1) × Weight</c> multiplier'lı penalty uyguluyor; gap büyüdükçe
/// lineer artar. V2 encoder bu davranışı taklit eder:
/// <c>effectiveGap = max(gapForwardAB, gapForwardBA, 0)</c>, eğer
/// <c>effectiveGap ≥ cap</c> ise <c>multiplier = effectiveGap − cap + 1</c>;
/// <c>sameDay ∧ triggered</c> olduğunda penalty <c>multiplier × S7Weight</c>.
/// </para>
/// <para>
/// NOTE (Plan 9): Yalnızca <see cref="ComponentType.Lecture"/> interval'ı iterate edilir;
/// lab interval ve multi-block lab gap deferred — S2/S4/S5 ile aynı kapsam kararı.
/// </para>
/// <para>
/// Section filtresi: <see cref="SectionInput.DepartmentId"/> + <see cref="SectionInput.Year"/>
/// her ikisi non-null olmalı; biri null ise bu section bir öğrenci-grup-bazlı pencerelemeye
/// dahil edilmez (curriculum mapping eksik). <see cref="SectionInput.TheoryHours"/> &gt; 0
/// crash guard'ı (<see cref="DecisionVariables.GetLectureInterval"/> theory-only döner).
/// </para>
/// <para>
/// Reification:
/// <list type="bullet">
/// <item><c>sameDay ⇔ dayA == dayB</c> — bidirectional.</item>
/// <item><c>gapAB ⇔ startB - endA &gt;= cap</c> (B sonra) — bidirectional.</item>
/// <item><c>gapBA ⇔ startA - endB &gt;= cap</c> (A sonra) — bidirectional.</item>
/// <item><c>gapAny ⇔ gapAB ∨ gapBA</c> — bidirectional.</item>
/// <item><c>penaltyLit ⇔ sameDay ∧ gapAny</c> — bidirectional.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class StudentGapEncoder : IConstraintEncoder
{
    public string Code => "S7";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;
        var t = ctx.Request.SoftThresholds;
        if (t.S7Weight <= 0) return;
        if (t.S7MaxGapSlots <= 0) return;

        // Group sections by (DepartmentId, Year); skip rows with null on either field.
        // Theory-only filter: GetLectureInterval döner null for lab-only sections (TheoryHours == 0).
        var groups = ctx.Request.Sections
            .Where(s => s.DepartmentId.HasValue && s.Year.HasValue && s.TheoryHours > 0)
            .GroupBy(s => (s.DepartmentId!.Value, s.Year!.Value))
            .ToList();

        foreach (var grp in groups)
        {
            var sectionList = grp.ToList();
            for (int i = 0; i < sectionList.Count; i++)
            {
                for (int j = i + 1; j < sectionList.Count; j++)
                {
                    var a = sectionList[i];
                    var b = sectionList[j];
                    var ivA = ctx.Variables.GetLectureInterval(a.Id);
                    var ivB = ctx.Variables.GetLectureInterval(b.Id);
                    if (ivA == null || ivB == null) continue;

                    var dayA = ctx.Variables.GetOrCreateComponentDayVar(
                        a.Id, ComponentType.Lecture, ctx.Request.TimeSlots);
                    var dayB = ctx.Variables.GetOrCreateComponentDayVar(
                        b.Id, ComponentType.Lecture, ctx.Request.TimeSlots);

                    // sameDay ⇔ dayA == dayB
                    var sameDay = ctx.Model.NewBoolVar($"s7_sameday_s{a.Id}_s{b.Id}");
                    ctx.Model.Add(dayA == dayB).OnlyEnforceIf(sameDay);
                    ctx.Model.Add(dayA != dayB).OnlyEnforceIf(sameDay.Not());

                    // Plan 13 V1 alignment: effectiveGap = max(0, gapForwardAB, gapForwardBA).
                    // gapForwardAB = ivB.Start - ivA.End (pozitif → B sonra)
                    // gapForwardBA = ivA.Start - ivB.End (pozitif → A sonra)
                    // İkisi de aynı anda pozitif olamaz (interval'ler overlap etmiyor);
                    // negatif olan dikkate alınmaz (max ile 0'a clamplenir).
                    var maxSpan = ctx.Request.TimeSlots.Count + 1;
                    var effectiveGap = ctx.Model.NewIntVar(0, maxSpan, $"s7_gap_s{a.Id}_s{b.Id}");
                    ctx.Model.AddMaxEquality(effectiveGap, new[]
                    {
                        ivB.StartExpr() - ivA.EndExpr(),
                        ivA.StartExpr() - ivB.EndExpr(),
                        LinearExpr.Constant(0),
                    });

                    // triggered ⇔ effectiveGap ≥ cap
                    var triggered = ctx.Model.NewBoolVar($"s7_trig_s{a.Id}_s{b.Id}");
                    ctx.Model.Add(effectiveGap >= t.S7MaxGapSlots).OnlyEnforceIf(triggered);
                    ctx.Model.Add(effectiveGap < t.S7MaxGapSlots).OnlyEnforceIf(triggered.Not());

                    // multiplier = effectiveGap - cap + 1 when triggered, else 0
                    var multiplier = ctx.Model.NewIntVar(0, maxSpan, $"s7_mult_s{a.Id}_s{b.Id}");
                    ctx.Model.Add(multiplier == effectiveGap - t.S7MaxGapSlots + 1)
                        .OnlyEnforceIf(triggered);
                    ctx.Model.Add(multiplier == 0).OnlyEnforceIf(triggered.Not());

                    // penalty = sameDay ? (multiplier × weight) : 0
                    var maxPenalty = (long)maxSpan * t.S7Weight;
                    var penalty = ctx.Model.NewIntVar(0, maxPenalty, $"s7_pen_s{a.Id}_s{b.Id}");
                    ctx.Model.Add(penalty == multiplier * (long)t.S7Weight)
                        .OnlyEnforceIf(sameDay);
                    ctx.Model.Add(penalty == 0).OnlyEnforceIf(sameDay.Not());
                    ctx.Penalties.Add(penalty);
                }
            }
        }
    }
}
