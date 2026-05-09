using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// DIST — DistributionConstraints encoder (Plan 8 real impl, Task 14 dispatch shell).
/// 6 type (SameDay/DifferentDay/SameRoom/BackToBack/NotBackToBack/Precedence)
/// × 2 tier (Unavailable=hard / Discouraged|StronglyDiscouraged=soft).
/// Iterates over <c>SolverRequest.DistributionConstraints</c>; PreferenceLevel switch'e göre
/// hard veya soft path. Helper method'lar pair-wise tekrarlanır (Tasks 15-19'da doldurulur).
///
/// NOTE (Plan 9 polish): <c>PreferenceLevel.Preferred</c> ve <c>StronglyPreferred</c> şu anda
/// <c>_ => -1</c> arm'ında atlanıyor. Plan 9'da pozitif bonus tier eklenirse switch
/// genişletilecek (negatif penalty / objective bonus).
///
/// NOTE (Plan 9 polish): <c>FirstOrDefault per CourseId</c> section anchor basitleştirmesi —
/// aynı kurs için birden fazla section varsa yalnızca ilki dist constraint'e katılır.
/// Production'da DistributionConstraints tablosu boş; admin feature'ı kullanmaya başlayınca
/// gerçek section seçim algoritması (multi-section enumeration / cohort eşleşmesi) Plan 9'da
/// değerlendirilecek.
///
/// NOTE (Plan 9 polish): Lab-only section guard — tüm per-type Encode*Pair method'ları
/// <c>TheoryHours == 0</c> (a veya b) olduğunda silently skip eder (lecture day var / lecture
/// room var oluşmadığından crash önlenir). Bu durum DIST constraint'in lab-only çiftlerde
/// hiç enforce edilmediği anlamına gelir. Multi-block lab (Plan 7b) day/room var'ları için
/// per-type semantik Plan 9'da ele alınacak.
/// </summary>
public sealed class DistributionConstraintEncoder : IConstraintEncoder
{
    public string Code => "DIST";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        var t = ctx.Request.SoftThresholds;

        foreach (var dist in ctx.Request.DistributionConstraints)
        {
            int? softWeight = dist.PreferenceLevel switch
            {
                PreferenceLevel.Unavailable => null,            // hard
                PreferenceLevel.Discouraged => t.DistDiscouragedWeight,
                PreferenceLevel.StronglyDiscouraged => t.DistStronglyDiscouragedWeight,
                _ => -1,                                         // skip Preferred/StronglyPreferred (Plan 9)
            };
            if (softWeight == -1) continue;

            // Resolve sections from courses (CourseId-based; FirstOrDefault per CourseId anchor).
            var sectionsForDist = dist.Courses
                .Select(c => ctx.Request.Sections.FirstOrDefault(s => s.CourseId == c.CourseId))
                .Where(s => s != null)
                .Cast<SectionInput>()
                .ToList();
            if (sectionsForDist.Count < 2) continue;

            // Pair-wise iteration — every (i, j) with i < j.
            for (int i = 0; i < sectionsForDist.Count; i++)
            {
                for (int j = i + 1; j < sectionsForDist.Count; j++)
                {
                    var a = sectionsForDist[i];
                    var b = sectionsForDist[j];
                    EncodePair(ctx, dist.Id, dist.DistributionType, a, b, softWeight);
                }
            }

            ctx.Assumptions.Register(Code, primarySectionId: dist.Id, secondarySectionId: null);
        }
    }

    private static void EncodePair(EncoderContext ctx, int distId, DistributionType type,
        SectionInput a, SectionInput b, int? softWeight)
    {
        switch (type)
        {
            case DistributionType.SameDay: EncodeSameDayPair(ctx, distId, a, b, softWeight); break;
            case DistributionType.DifferentDay: EncodeDifferentDayPair(ctx, distId, a, b, softWeight); break;
            case DistributionType.SameRoom: EncodeSameRoomPair(ctx, distId, a, b, softWeight); break;
            case DistributionType.BackToBack: EncodeBackToBackPair(ctx, distId, a, b, softWeight); break;
            case DistributionType.NotBackToBack: EncodeNotBackToBackPair(ctx, distId, a, b, softWeight); break;
            case DistributionType.Precedence: EncodePrecedencePair(ctx, distId, a, b, softWeight); break;
        }
    }

    /// <summary>
    /// SameDay: lecture component'leri aynı güne düşmeli.
    /// Hard (softWeight==null): <c>dayA == dayB</c>.
    /// Soft: reified <c>diffLit ⇔ (dayA != dayB)</c>; penalty = <c>diffLit × softWeight</c>.
    /// TheoryHours==0 olan section (lab-only) lecture day var alamaz → guard ile skip.
    /// </summary>
    private static void EncodeSameDayPair(EncoderContext ctx, int distId, SectionInput a, SectionInput b, int? softWeight)
    {
        if (a.TheoryHours == 0 || b.TheoryHours == 0) return;

        var dayA = ctx.Variables.GetOrCreateComponentDayVar(a.Id, ComponentType.Lecture, ctx.Request.TimeSlots);
        var dayB = ctx.Variables.GetOrCreateComponentDayVar(b.Id, ComponentType.Lecture, ctx.Request.TimeSlots);

        if (softWeight == null)
        {
            // Hard: same day enforced.
            ctx.Model.Add(dayA == dayB);
        }
        else
        {
            // Soft: penalty when different.
            var diffLit = ctx.Model.NewBoolVar($"dist{distId}_sd_diff_{a.Id}_{b.Id}");
            ctx.Model.Add(dayA != dayB).OnlyEnforceIf(diffLit);
            ctx.Model.Add(dayA == dayB).OnlyEnforceIf(diffLit.Not());
            ctx.Penalties.Add(LinearExpr.Term(diffLit, (long)softWeight.Value));
        }
    }
    private static void EncodeDifferentDayPair(EncoderContext ctx, int distId, SectionInput a, SectionInput b, int? softWeight) { }
    private static void EncodeSameRoomPair(EncoderContext ctx, int distId, SectionInput a, SectionInput b, int? softWeight) { }
    private static void EncodeBackToBackPair(EncoderContext ctx, int distId, SectionInput a, SectionInput b, int? softWeight) { }
    private static void EncodeNotBackToBackPair(EncoderContext ctx, int distId, SectionInput a, SectionInput b, int? softWeight) { }
    private static void EncodePrecedencePair(EncoderContext ctx, int distId, SectionInput a, SectionInput b, int? softWeight) { }
}
