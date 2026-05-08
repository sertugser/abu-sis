using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H-DO — Instructor Day Off. Her hoca en az 1 boş workday almalı.
/// v1 paritesi (<c>NoDayOffEvaluator.cs</c>): <c>usedDays &gt;= 5</c> → infeasible.
/// Plan 5 generalizasyonu: <c>usedDays &gt; workdays.Count - 1</c> → infeasible
/// (5 günlük hafta için aynı semantik; 4-/6-günlük takvimler için doğru genelleme).
/// CP-SAT model: per (instructor, workday) reified <c>hasClass[i,d]</c> BoolVar = section'lardan
/// herhangi birinin component'i o gün başlıyor mu (OR). Constraint:
/// <c>sum(hasClass[i, d] for d in workdays) ≤ workdays.Count - 1</c>.
/// 1 assumption per instructor (constraint tek quantifier — "en az 1 gün off").
/// </summary>
public sealed class NoDayOffEncoder : IConstraintEncoder
{
    public string Code => "H-DO";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;
        if (ctx.Request.TimeSlots.Count == 0)
            return;

        // Distinct workdays from TimeSlot grid (typically Mon-Fri = 1..5).
        var workdays = ctx.Request.TimeSlots
            .Select(s => (int)s.DayOfWeek)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        // Pathological skip: 0 or 1 workday means "≥ 1 day off" is undefined or forces
        // every instructor to have 0 sections — meaningless. ≥2 workdays: enforce normally.
        // (Plan 5 spec originally had `<= 4` skip; revised after code review since 4-workday
        // calendars also need H-DO enforcement.)
        if (workdays.Count <= 1)
            return;

        var maxAllowedDays = workdays.Count - 1;

        var byInstructor = ctx.Request.Sections
            .Where(s => s.InstructorId.HasValue)
            .GroupBy(s => s.InstructorId!.Value);

        foreach (var group in byInstructor)
        {
            var instructorId = group.Key;
            var sectionList = group.ToList();

            var hasClassVars = new List<BoolVar>();

            foreach (var day in workdays)
            {
                var anyComponentMatches = new List<ILiteral>();

                foreach (var section in sectionList)
                {
                    // GetSectionPresent always returns BoolVar (which is IntVar) in current impl;
                    // BoolVar cast required for Not() in reification helper.
                    if (ctx.Variables.GetSectionPresent(section.Id) is not BoolVar present)
                        continue;

                    if (section.TheoryHours > 0)
                    {
                        var theoryDayVar = ctx.Variables.GetOrCreateComponentDayVar(
                            section.Id, ComponentType.Lecture, ctx.Request.TimeSlots);
                        anyComponentMatches.Add(ReifyDayMatchAndPresent(ctx.Model, theoryDayVar, day, present,
                            $"do_lec_s{section.Id}_i{instructorId}_d{day}"));
                    }
                    if (section.LabHours > 0)
                    {
                        var labDayVar = ctx.Variables.GetOrCreateComponentDayVar(
                            section.Id, ComponentType.Lab, ctx.Request.TimeSlots);
                        anyComponentMatches.Add(ReifyDayMatchAndPresent(ctx.Model, labDayVar, day, present,
                            $"do_lab_s{section.Id}_i{instructorId}_d{day}"));
                    }
                }

                if (anyComponentMatches.Count == 0)
                    continue;

                var hasClass = ctx.Model.NewBoolVar($"do_hasClass_i{instructorId}_d{day}");
                // hasClass ⇔ OR(anyComponentMatches)
                // Forward: hasClass → OR(matches)
                ctx.Model.AddBoolOr(anyComponentMatches).OnlyEnforceIf(hasClass);
                // Reverse: each match → hasClass
                foreach (var lit in anyComponentMatches)
                    ctx.Model.AddImplication(lit, hasClass);
                hasClassVars.Add(hasClass);
            }

            if (hasClassVars.Count == 0)
                continue;

            var literal = ctx.Assumptions.Register(Code,
                primarySectionId: instructorId,
                secondarySectionId: null);
            ctx.Model.Add(LinearExpr.Sum(hasClassVars) <= maxAllowedDays).OnlyEnforceIf(literal);
        }
    }

    /// <summary>
    /// Returns a BoolVar literal equivalent to <c>(dayVar == day) ∧ present</c>.
    /// Standard CP-SAT reification pattern (mirrors DailyLoadEncoder.ReifyDayMatchAndPresent).
    /// </summary>
    private static BoolVar ReifyDayMatchAndPresent(CpModel model, IntVar dayVar, int day,
        BoolVar present, string name)
    {
        var dayEq = model.NewBoolVar($"{name}_dayEq");
        model.Add(dayVar == day).OnlyEnforceIf(dayEq);
        model.Add(dayVar != day).OnlyEnforceIf(dayEq.Not());

        var combined = model.NewBoolVar(name);
        model.AddBoolAnd(new ILiteral[] { dayEq, present }).OnlyEnforceIf(combined);
        model.AddBoolOr(new ILiteral[] { dayEq.Not(), present.Not() }).OnlyEnforceIf(combined.Not());
        return combined;
    }
}
