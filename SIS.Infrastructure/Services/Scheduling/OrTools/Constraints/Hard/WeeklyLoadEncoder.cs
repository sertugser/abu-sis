using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H-WL — Instructor Weekly Load. Hocanın haftalık toplam ders saati limiti aşamaz.
/// Limit: section.InstructorMaxWeeklyHours > 0 ? override : DefaultInstructorMaxWeeklyHours.
/// 0 (override + default ikisi de 0) → constraint disabled (v1 paritesi).
/// CP-SAT: sum(present[s] * (theoryHours[s] + labHours[s])) ≤ limit per instructor.
/// UNSAT core: per-instructor 1 assumption literal (primary = instructorId).
/// </summary>
public sealed class WeeklyLoadEncoder : IConstraintEncoder
{
    public string Code => "H-WL";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        var byInstructor = ctx.Request.Sections
            .Where(s => s.InstructorId.HasValue)
            .GroupBy(s => s.InstructorId!.Value);

        foreach (var group in byInstructor)
        {
            var instructorId = group.Key;
            var sectionList = group.ToList();

            // Resolve max hours: section override > request default > 0 (disabled)
            var overrideMax = sectionList.Max(s => s.InstructorMaxWeeklyHours);
            var maxHours = overrideMax > 0 ? overrideMax : ctx.Request.DefaultInstructorMaxWeeklyHours;
            if (maxHours <= 0) continue; // disabled

            // Build sum(present * (theory + lab))
            var terms = new List<LinearExpr>();
            foreach (var section in sectionList)
            {
                if (ctx.Variables.GetSectionPresent(section.Id) is not IntVar present)
                    continue;
                var hours = section.TheoryHours + section.LabHours;
                if (hours <= 0) continue;
                terms.Add(LinearExpr.Term(present, hours));
            }
            if (terms.Count == 0) continue;

            var literal = ctx.Assumptions.Register(Code,
                primarySectionId: instructorId,
                secondarySectionId: null);
            ctx.Model.Add(LinearExpr.Sum(terms) <= maxHours).OnlyEnforceIf(literal);
        }
    }
}
