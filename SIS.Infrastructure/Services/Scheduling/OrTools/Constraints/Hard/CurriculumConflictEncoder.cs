using Google.OrTools.Sat;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H5 — Curriculum Conflict. Aynı (ProgramId, Year) grubuna ait section'ların lecture interval'ları
/// çakışamaz; çünkü o yılın öğrencileri tüm bu derslere kayıtlı olabilir.
/// CP-SAT model: Per-group AddNoOverlap on lecture intervals; assumption literal per group.
/// Scope: Plan 2 Task 7 only handles lecture intervals; lab grouping (alternative lab groups)
/// is out of scope for now.
/// Sections without ProgramId or Year are skipped (treated as ungrouped).
/// </summary>
public sealed class CurriculumConflictEncoder : IConstraintEncoder
{
    public string Code => "H5";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        var groups = ctx.Request.Sections
            .Where(s => s.ProgramId.HasValue && s.Year.HasValue)
            .GroupBy(s => (s.ProgramId!.Value, s.Year!.Value));

        foreach (var group in groups)
        {
            var lectureIntervals = group
                .Where(s => s.TheoryHours > 0)
                .Select(s => ctx.Variables.GetLectureInterval(s.Id))
                .Where(iv => iv != null)
                .Cast<IntervalVar>()
                .ToList();

            if (lectureIntervals.Count >= 2)
            {
                ctx.Model.AddNoOverlap(lectureIntervals);
                // Encode group as (ProgramId, Year) in assumption metadata
                ctx.Assumptions.Register(Code,
                    primarySectionId: group.Key.Item1,   // ProgramId
                    secondarySectionId: group.Key.Item2); // Year
            }
        }
    }
}
