namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H10 — Faculty Classroom. Section'ın derslik'i fakülteye atanmış derslikler arasında olmalı.
/// Bu kısıt aslında <see cref="Model.DecisionVariables.CreateRoomVariable"/> tarafından zaten
/// uygulanıyor — room IntVar'ın domain'i `SolverRequest.Classrooms` listesindeki id'lerle sınırlı.
/// SolverRequestBuilder bu listeyi fakülte filtreli yükler.
///
/// Bu encoder constraint EKLEMEZ; sadece UNSAT core diagnostic'i için per-section assumption
/// literal kaydeder. INFEASIBLE durumda "fakültenin atanmış sınıfı yetmedi" tipi raporlama
/// için kullanılır.
/// </summary>
public sealed class FacultyClassroomEncoder : IConstraintEncoder
{
    public string Code => "H10";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        // Per-section diagnostic marker. No actual constraint added — domain restriction in
        // CreateRoomVariable handles enforcement; this just exposes the constraint code to UNSAT core.
        foreach (var section in ctx.Request.Sections)
        {
            ctx.Assumptions.Register(Code, primarySectionId: section.Id, secondarySectionId: null);
        }
    }
}
