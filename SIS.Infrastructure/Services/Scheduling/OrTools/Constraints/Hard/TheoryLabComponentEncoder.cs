namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H6 — Theory-Lab Component Conflict. Aynı section'ın teori ve lab interval'ları çakışamaz.
/// Bir öğrenci/hoca aynı anda iki yerde olamaz.
/// CP-SAT model: Per-section AddNoOverlap on (theory_interval, lab_interval).
/// Sections with only theory or only lab are skipped (no two-component overlap possible).
/// </summary>
public sealed class TheoryLabComponentEncoder : IConstraintEncoder
{
    public string Code => "H6";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        foreach (var section in ctx.Request.Sections)
        {
            if (section.TheoryHours <= 0 || section.LabHours <= 0)
                continue;

            var theory = ctx.Variables.GetLectureInterval(section.Id);
            var lab = ctx.Variables.GetLabInterval(section.Id);
            if (theory == null || lab == null)
                continue;

            ctx.Model.AddNoOverlap(new[] { theory, lab });
            ctx.Assumptions.Register(Code, primarySectionId: section.Id, secondarySectionId: null);
        }
    }
}
