using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// H-DO — NoDayOff soft mode (Plan 10 Phase 3).
/// Hard encoder her hocanın en az 1 boş workday almasını zorluyor; soft modda boş gün yoksa
/// <see cref="SoftThresholds.HDOWeight"/> penalty eklenir (admin: "uygunsa 5/5 yüklenebilir
/// ama tercih ederiz boş gün olsun" semantiği).
/// </summary>
/// <remarks>
/// Pattern: hard encoder'ın <c>hasClass[i, d]</c> reified BoolVar'lerini paralel kuran
/// (her instructor × workday için reified "o gün ders var mı") yapı, sonra
/// <c>noDayOff = AND(hasClass[i, d] for all d)</c> reified BoolVar × weight penalty.
/// </remarks>
public sealed class NoDayOffSoftEncoder : IConstraintEncoder
{
    public string Code => "H-DO";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Hard");
        if (!string.Equals(category, "Soft", StringComparison.OrdinalIgnoreCase))
            return;

        var weight = (long)ctx.Request.SoftThresholds.HDOWeight;
        if (weight <= 0) return;

        if (ctx.Request.TimeSlots.Count == 0) return;

        var workdays = ctx.Request.TimeSlots
            .Select(s => (int)s.DayOfWeek)
            .Distinct()
            .OrderBy(d => d)
            .ToList();
        if (workdays.Count <= 1) return;

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
                    if (ctx.Variables.GetSectionPresent(section.Id) is not BoolVar present)
                        continue;

                    if (section.TheoryHours > 0)
                    {
                        var theoryDayVar = ctx.Variables.GetOrCreateComponentDayVar(
                            section.Id, ComponentType.Lecture, ctx.Request.TimeSlots);
                        anyComponentMatches.Add(ReifyDayMatchAndPresent(ctx.Model, theoryDayVar, day, present,
                            $"hdosoft_lec_s{section.Id}_i{instructorId}_d{day}"));
                    }
                    if (section.LabHours > 0)
                    {
                        var labDayVar = ctx.Variables.GetOrCreateComponentDayVar(
                            section.Id, ComponentType.Lab, ctx.Request.TimeSlots);
                        anyComponentMatches.Add(ReifyDayMatchAndPresent(ctx.Model, labDayVar, day, present,
                            $"hdosoft_lab_s{section.Id}_i{instructorId}_d{day}"));
                    }
                }

                if (anyComponentMatches.Count == 0) continue;

                var hasClass = ctx.Model.NewBoolVar($"hdosoft_hasClass_i{instructorId}_d{day}");
                ctx.Model.AddBoolOr(anyComponentMatches).OnlyEnforceIf(hasClass);
                foreach (var lit in anyComponentMatches)
                    ctx.Model.AddImplication(lit, hasClass);
                hasClassVars.Add(hasClass);
            }

            if (hasClassVars.Count == 0) continue;

            // noDayOff ⇔ AND(hasClass[d] for all d) — i.e. instructor works every workday.
            var noDayOff = ctx.Model.NewBoolVar($"hdosoft_noDayOff_i{instructorId}");
            ctx.Model.AddBoolAnd(hasClassVars.Cast<ILiteral>().ToArray()).OnlyEnforceIf(noDayOff);
            ctx.Model.AddBoolOr(hasClassVars.Select(b => (ILiteral)b.Not()).ToArray())
                .OnlyEnforceIf(noDayOff.Not());

            ctx.Penalties.Add(LinearExpr.Term(noDayOff, weight));
        }
    }

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
