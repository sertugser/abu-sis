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
/// NOTE (Plan 9): v1 evaluator per-(dept, year, day) ardışık section sıralaması üzerinden
/// her ardışık çift için <c>(gap - cap + 1) × Weight</c> penalty (multiplier'lı) uygular —
/// CP-SAT'ta sıralama IntVar zincirleri pahalı. Plan 8 simplification: per-pair reified
/// <c>same_day ∧ (gapAB OR gapBA)</c> literal'i, penalty sabit <see cref="SoftThresholds.S7Weight"/>.
/// "Birden fazla aynı-gün-uzak ders" durumu pair-bazlı sayım ile yine de cezalandırılır
/// (3 section aynı gün uzaksa 3 pair = 3 × Weight).
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

                    // gapAB ⇔ (startB - endA >= cap) — B is after A with ≥ cap empty slots between.
                    var gapAB = ctx.Model.NewBoolVar($"s7_gapAB_s{a.Id}_s{b.Id}");
                    ctx.Model.Add(ivB.StartExpr() - ivA.EndExpr() >= t.S7MaxGapSlots)
                        .OnlyEnforceIf(gapAB);
                    ctx.Model.Add(ivB.StartExpr() - ivA.EndExpr() < t.S7MaxGapSlots)
                        .OnlyEnforceIf(gapAB.Not());

                    // gapBA ⇔ (startA - endB >= cap) — symmetric, A is after B.
                    var gapBA = ctx.Model.NewBoolVar($"s7_gapBA_s{a.Id}_s{b.Id}");
                    ctx.Model.Add(ivA.StartExpr() - ivB.EndExpr() >= t.S7MaxGapSlots)
                        .OnlyEnforceIf(gapBA);
                    ctx.Model.Add(ivA.StartExpr() - ivB.EndExpr() < t.S7MaxGapSlots)
                        .OnlyEnforceIf(gapBA.Not());

                    // gapAny ⇔ gapAB ∨ gapBA
                    var gapAny = ctx.Model.NewBoolVar($"s7_gapAny_s{a.Id}_s{b.Id}");
                    ctx.Model.AddBoolOr(new ILiteral[] { gapAB, gapBA }).OnlyEnforceIf(gapAny);
                    ctx.Model.AddBoolAnd(new ILiteral[] { gapAB.Not(), gapBA.Not() })
                        .OnlyEnforceIf(gapAny.Not());

                    // penaltyLit ⇔ sameDay ∧ gapAny
                    var penaltyLit = ctx.Model.NewBoolVar($"s7_pen_s{a.Id}_s{b.Id}");
                    ctx.Model.AddBoolAnd(new ILiteral[] { sameDay, gapAny })
                        .OnlyEnforceIf(penaltyLit);
                    ctx.Model.AddBoolOr(new ILiteral[] { sameDay.Not(), gapAny.Not() })
                        .OnlyEnforceIf(penaltyLit.Not());

                    // (long) cast: defensive against future S7Weight tuning that might overflow int
                    // when summed across many pair-violations.
                    ctx.Penalties.Add(LinearExpr.Term(penaltyLit, (long)t.S7Weight));
                }
            }
        }
    }
}
