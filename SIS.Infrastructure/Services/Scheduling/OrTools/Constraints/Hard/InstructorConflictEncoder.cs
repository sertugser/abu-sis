using Google.OrTools.Sat;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H2 - Instructor Conflict. Aynı hocaya atanmış section'ların interval'ları çakışamaz.
/// CP-SAT model: per-instructor AddNoOverlap over [lectureInterval, labInterval] of assigned sections.
/// Multi-group lab section: tek lab interval (paralel lab'lar aynı zaman, farklı oda -> instructor için fark etmez).
/// Section.InstructorId == null olan section'lar atlanır.
/// UNSAT core: per-instructor 1 assumption literal (primary = instructorId).
/// </summary>
public sealed class InstructorConflictEncoder : IConstraintEncoder
{
    public string Code => "H2";
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
            var intervals = new List<IntervalVar>();

            foreach (var section in group)
            {
                if (section.TheoryHours > 0)
                {
                    var iv = ctx.Variables.GetLectureInterval(section.Id);
                    if (iv != null) intervals.Add(iv);
                }
                if (section.LabHours > 0)
                {
                    // Çoklu-blok lab: tüm lab blokları instructor no-overlap'a girmeli (yalnız blok 0
                    // değil). Aksi halde 4 saatlik (2 blok) lab'ın 2. bloğu hocanın başka dersiyle
                    // çakışabilir ve evaluator (H2) bunu raporlarken solver "geçerli" derdi.
                    foreach (var labIv in ctx.Variables.GetLabIntervals(section.Id))
                        intervals.Add(labIv);
                }
            }

            // >=2 interval needed (single interval = no possible collision)
            if (intervals.Count < 2) continue;

            ctx.Model.AddNoOverlap(intervals);
            ctx.Assumptions.Register(Code,
                primarySectionId: instructorId,    // overloaded: instructor id as primary for diagnostics
                secondarySectionId: null);
        }
    }
}
