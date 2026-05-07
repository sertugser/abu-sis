namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H11 — Corequisite. Eşkoşul ders'ler aynı dönemde sunulmalı.
/// Bu kısıt aslında SolverRequestBuilder'da (Plan 2 Task 17) input validation
/// olarak uygulanır — eşkoşul kursu olmayan section'lar input'a alınmaz.
///
/// Bu encoder constraint EKLEMEZ; sadece per-corequisite-relationship assumption
/// literal kaydederek UnsatCoreInterpreter'ın H11 ihlallerini tanımasını sağlar.
/// </summary>
public sealed class CorequisiteEncoder : IConstraintEncoder
{
    public string Code => "H11";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        foreach (var rel in ctx.Request.Corequisites)
        {
            ctx.Assumptions.Register(Code,
                primarySectionId: rel.PrimaryCourseId,
                secondarySectionId: rel.CorequisiteCourseId);
        }
    }
}
