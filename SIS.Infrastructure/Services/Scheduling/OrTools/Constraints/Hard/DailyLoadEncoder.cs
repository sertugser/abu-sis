using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H-DL — Instructor Daily Load. Hocanın bir gündeki toplam ders saati limiti aşamaz (default 9h).
/// CP-SAT model: per-section, per-component reified BoolVar
/// <c>dayMatchAndPresent ⇔ (componentDayVar == day) ∧ present</c>; sonra
/// <c>sum(dayMatchAndPresent × hours) ≤ maxDailyHours</c> her (instructor, day) için.
/// 1 assumption per (instructor, day) — diagnostic specific to that day
/// (<c>secondarySectionId = day</c>) so UnsatCoreInterpreter can attribute violations to weekdays.
/// <c>Schedule.MaxDailyHoursPerInstructor</c> SystemConfig'ten okunur (factory DI).
/// </summary>
public sealed class DailyLoadEncoder : IConstraintEncoder
{
    public string Code => "H-DL";
    public bool IsHard => true;

    private readonly int _maxDailyHours;

    public DailyLoadEncoder(int maxDailyHours = 9)
    {
        if (maxDailyHours <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxDailyHours),
                maxDailyHours, "maxDailyHours must be positive.");
        _maxDailyHours = maxDailyHours;
    }

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

        var byInstructor = ctx.Request.Sections
            .Where(s => s.InstructorId.HasValue)
            .GroupBy(s => s.InstructorId!.Value);

        foreach (var group in byInstructor)
        {
            var instructorId = group.Key;
            var sectionList = group.ToList();

            foreach (var day in workdays)
            {
                var terms = new List<LinearExpr>();

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
                        var lit = ReifyDayMatchAndPresent(ctx.Model, theoryDayVar, day, present,
                            $"dl_lec_s{section.Id}_d{day}");
                        terms.Add(LinearExpr.Term(lit, section.TheoryHours));
                    }
                    if (section.LabHours > 0)
                    {
                        var labDayVar = ctx.Variables.GetOrCreateComponentDayVar(
                            section.Id, ComponentType.Lab, ctx.Request.TimeSlots);
                        var lit = ReifyDayMatchAndPresent(ctx.Model, labDayVar, day, present,
                            $"dl_lab_s{section.Id}_d{day}");
                        terms.Add(LinearExpr.Term(lit, section.LabHours));
                    }
                }

                if (terms.Count == 0)
                    continue;

                var literal = ctx.Assumptions.Register(Code,
                    primarySectionId: instructorId,
                    secondarySectionId: day);
                ctx.Model.Add(LinearExpr.Sum(terms) <= _maxDailyHours).OnlyEnforceIf(literal);
            }
        }
    }

    /// <summary>
    /// Returns a BoolVar literal equivalent to <c>(dayVar == day) ∧ present</c>.
    /// Standard CP-SAT reification pattern:
    /// - <c>combined ⇒ dayEq ∧ present</c> via <c>AddBoolAnd(...).OnlyEnforceIf(combined)</c>
    /// - <c>¬combined ⇒ ¬dayEq ∨ ¬present</c> via <c>AddBoolOr(...).OnlyEnforceIf(combined.Not())</c>
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
