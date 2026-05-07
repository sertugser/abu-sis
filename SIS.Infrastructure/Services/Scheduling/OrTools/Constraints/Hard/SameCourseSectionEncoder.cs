using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H8 — Same Course Section Conflict. Aynı dersin farklı şubeleri çakışamaz.
/// Öğrenciler herhangi bir şubeyi seçebileceği için tüm şubelerin saatlerinin farklı olması gerekir.
/// CP-SAT model: Per-course AddNoOverlap on lecture intervals AND lab intervals.
/// UNSAT core: per-pair assumption literals for fine-grained diagnostics.
/// </summary>
public sealed class SameCourseSectionEncoder : IConstraintEncoder
{
    public string Code => "H8";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        var byCourse = ctx.Request.Sections.GroupBy(s => s.CourseId);

        foreach (var courseGroup in byCourse)
        {
            // Lecture group within this course
            var lectureSections = courseGroup
                .Where(s => s.TheoryHours > 0)
                .ToList();
            EncodeGroup(ctx, lectureSections, useLectureInterval: true);

            // Lab group within this course
            var labSections = courseGroup
                .Where(s => s.LabHours > 0)
                .ToList();
            EncodeGroup(ctx, labSections, useLectureInterval: false);
        }
    }

    private void EncodeGroup(EncoderContext ctx, IReadOnlyList<SectionInput> sections, bool useLectureInterval)
    {
        if (sections.Count < 2)
            return;

        var intervals = new List<IntervalVar>();
        foreach (var s in sections)
        {
            var iv = useLectureInterval
                ? ctx.Variables.GetLectureInterval(s.Id)
                : ctx.Variables.GetLabInterval(s.Id);
            if (iv != null)
                intervals.Add(iv);
        }

        if (intervals.Count < 2)
            return;

        ctx.Model.AddNoOverlap(intervals);

        // Per-pair assumption literals for fine-grained UNSAT core
        for (int i = 0; i < sections.Count; i++)
        {
            for (int j = i + 1; j < sections.Count; j++)
            {
                ctx.Assumptions.Register(Code, sections[i].Id, sections[j].Id);
            }
        }
    }
}
