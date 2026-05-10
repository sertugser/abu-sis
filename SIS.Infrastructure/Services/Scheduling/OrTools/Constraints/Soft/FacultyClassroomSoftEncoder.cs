using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// H10 — FacultyClassroom soft mode (Plan 10 Phase 3 — STUB).
/// Hard "encoder" aslında <see cref="DecisionVariables.CreateRoomVariable"/>'in oda IntVar
/// domain'ini fakülte sınırlarıyla daraltması üzerinden çalışıyor. Soft moda geçmek için
/// domain'i tüm fakültelerin odalarına genişletmek + dışarı çıkışları reified literal ile
/// penalty'ye bağlamak gerekir.
/// </summary>
/// <remarks>
/// <para>
/// Domain genişletme zaman ve solver kararlılığı açısından maliyetli; SolverRequestBuilder
/// classroom listesini fakülte-filtreli yüklediği için soft mod gerçek penalty üretemez —
/// soft mod default Hard semantiğini bypass ettiğinde domain hâlâ sadece fakültenin oda
/// id'lerini içeriyor. Gerçek soft semantiği için iki yol var:
///   1. SolverRequestBuilder'a "include all-faculty rooms when H10 Category=Soft" flag'i
///      eklemek (request shape değişir).
///   2. ScheduleModelBuilder içinde Encoder.Encode öncesinde room IntVar domain'ini
///      yeniden kurmak (büyük refactor).
/// </para>
/// <para>
/// Plan 10+ implement plan: yukarıdaki seçenek 1'i tercih et — request shape minimal
/// değişiklik, builder fakülte filtresinden vazgeçer, hard encoder per (section, classroom)
/// fakülte-dışı pair'ler için reified penalty literal'i kaydeder. Şu an STUB: hard mode
/// default; soft mode admin override edebilir ama davranışsal etkisi yok (domain hâlâ
/// kısıtlı).
/// </para>
/// </remarks>
public sealed class FacultyClassroomSoftEncoder : IConstraintEncoder
{
    public string Code => "H10";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Hard");
        if (!string.Equals(category, "Soft", StringComparison.OrdinalIgnoreCase))
            return;

        // STUB: gerçek soft logic Plan 10+ (domain expansion gerektirir).
        // Şu an penalty üretilmez; admin Soft toggle çalışır ama pratikte kural pasif.
    }
}
