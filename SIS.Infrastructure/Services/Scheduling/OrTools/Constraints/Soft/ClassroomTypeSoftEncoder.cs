using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// H4 — ClassroomType soft mode (Plan 10 Phase 3 — STUB).
/// Hard encoder lecture/lab room IntVar'ı için per (section, classroom) tip-uyumsuz çiftleri
/// <c>roomVar != classroom.Id</c> ile dışlıyor. Soft mode için bu yasakları kaldırıp
/// "tip uyumsuzsa penalty" reified literal'lerine dönüştürmek gerekir.
/// </summary>
/// <remarks>
/// <para>
/// Şu anki <see cref="DecisionVariables.CreateRoomVariable"/> her classroom id'yi domain'e
/// atıyor (faculty filter ScheduleModelBuilder'da uygulanıyor). Tip uyumsuz oda gerçekten
/// kullanıldıysa <c>roomVar == classroom.Id</c> reified BoolVar üreterek per pair penalty
/// eklenebilir — fakat aynı section'ın multi-group lab atamaları (parallel labs) ve
/// theory lecture room'u ayrı IntVar'lar; her IntVar × her tip-uyumsuz classroom için
/// reified BoolVar üretmek model boyutunu ciddi büyütür (≈ sections × classrooms × 2).
/// </para>
/// <para>
/// Plan 10+ implement plan: hard encoder'ın <c>EncodeLabComponent</c>/<c>EncodeTheoryComponent</c>
/// içinde kurduğu uyumsuzluk-listesini paralel toplayıp Soft modunda <c>roomVar == cid</c>
/// reified literal × <see cref="SoftThresholds.H4Weight"/> şeklinde penalty kaydet. Ek olarak
/// AmphiThreshold ve ClassroomTypeRequirement gate'lerini Soft modda da işle (false ise no-op).
/// Şu an STUB: hard encoder bypass olduğunda kural fiilen kapalı (ban yok, penalty da yok).
/// </para>
/// </remarks>
public sealed class ClassroomTypeSoftEncoder : IConstraintEncoder
{
    public string Code => "H4";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Hard");
        if (!string.Equals(category, "Soft", StringComparison.OrdinalIgnoreCase))
            return;

        // STUB: gerçek soft logic Plan 10+. Hard mode default; soft mode admin override
        // edebilir ama penalty üretmez (kural pratikte kapalı). Build/test infrastructure
        // hazır, gerçek penalty ileri planda.
    }
}
