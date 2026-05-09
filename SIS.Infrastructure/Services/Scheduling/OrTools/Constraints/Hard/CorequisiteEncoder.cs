namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H11 — Corequisite. Eşkoşul ders'ler aynı dönemde sunulmalı.
/// Bu kısıt aslında SolverRequestBuilder'da (Plan 2 Task 17) input validation
/// olarak uygulanır — eşkoşul kursu olmayan section'lar input'a alınmaz.
///
/// Bu encoder constraint EKLEMEZ; sadece per-corequisite-relationship assumption
/// literal kaydederek UnsatCoreInterpreter'ın H11 ihlallerini tanımasını sağlar.
///
/// Plan 6 Task 8 SolverRequest.Corequisites'i CoursePrerequisites WHERE IsCorequisite=true
/// satırlarıyla doldurmaya başladı; encoder bu Plan 6 itibariyle real-mode (önceden Corequisites
/// her zaman boş listeydi → silent no-op). Real "non-conflicting pair exists" enforcement
/// (v1 paritesi) Plan 6.5'a ertelendi — şu an enumerate-only diagnostic.
/// </summary>
public sealed class CorequisiteEncoder : IConstraintEncoder
{
    public string Code => "H11";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        // 2026-05-09: corequisite real enforcement (non-conflicting pair must exist) henüz
        // yapılmadı — Plan 6.5'a ertelendi. Production'da DB'de 0 corequisite çifti olduğu için
        // bu sessizce no-op'tu. Admin DB'ye corequisite çifti eklerse bu encoder pair'in
        // çakışmamasını GARANTİ ETMEZ → sessiz veri hatası riski. Net hata fırlat.
        if (ctx.Request.Corequisites.Count > 0)
        {
            throw new NotSupportedException(
                $"Corequisite encoder real enforcement henüz yapılmadı, ancak {ctx.Request.Corequisites.Count} corequisite çifti DB'de tanımlı. " +
                "Sessiz veri hatasını önlemek için scheduler bloklandı. Çözüm: corequisite çiftlerini geçici olarak kaldırın veya manuel atama yapın.");
        }

        foreach (var rel in ctx.Request.Corequisites)
        {
            ctx.Assumptions.Register(Code,
                primarySectionId: rel.PrimaryCourseId,
                secondarySectionId: rel.CorequisiteCourseId);
        }
    }
}
