using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H17 — Slot AllowedMode Restriction. Section.TeachingMethod ile slot.AllowedMode arasında
/// simetrik kısıt:
/// <list type="bullet">
///   <item>FaceToFace section → OnlineOnly slot kullanamaz.</item>
///   <item>Online section → FaceToFaceOnly slot kullanamaz (Plan 9 Task 12 simetri polish).</item>
///   <item>Hybrid section veya Both slot → kısıt yok.</item>
/// </list>
/// CP-SAT model: Yasaklı slot indeksleri için lecture/lab interval start IntVar'larına
/// <c>!= slotIdx</c> assumption-gated literal'i eklenir. Multi-block lab desteği için
/// <see cref="DecisionVariables.GetLabIntervals"/> plural accessor (Issue 3 paterni).
/// </summary>
public sealed class FaceToFaceSlotEncoder : IConstraintEncoder
{
    public string Code => "H17";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        var faceToFaceOnlySlots = new List<int>();
        var onlineOnlySlots = new List<int>();
        for (int i = 0; i < ctx.Request.TimeSlots.Count; i++)
        {
            var mode = ctx.Request.TimeSlots[i].AllowedMode;
            if (mode == TimeSlotAllowedMode.FaceToFaceOnly) faceToFaceOnlySlots.Add(i);
            else if (mode == TimeSlotAllowedMode.OnlineOnly) onlineOnlySlots.Add(i);
        }

        if (faceToFaceOnlySlots.Count == 0 && onlineOnlySlots.Count == 0)
            return;

        foreach (var section in ctx.Request.Sections)
        {
            // FaceToFace section → OnlineOnly slot ban
            if (section.TeachingMethod == TeachingMethod.FaceToFace && onlineOnlySlots.Count > 0)
                BanSlots(ctx, section, onlineOnlySlots);

            // Plan 9 Task 12 — Online section → FaceToFaceOnly slot ban (simetri polish)
            if (section.TeachingMethod == TeachingMethod.Online && faceToFaceOnlySlots.Count > 0)
                BanSlots(ctx, section, faceToFaceOnlySlots);
        }
    }

    /// <summary>
    /// Section'ın lecture + lab interval'larının start IntVar'ı yasaklı slot indekslerine
    /// eşit olamaz. Multi-block lab için her block'a ayrı kısıt eklenir.
    /// </summary>
    private void BanSlots(EncoderContext ctx, SectionInput section, IReadOnlyList<int> forbiddenIndices)
    {
        // Lecture component
        if (section.TheoryHours > 0)
        {
            var iv = ctx.Variables.GetLectureInterval(section.Id);
            BanForInterval(ctx, section.Id, iv, forbiddenIndices);
        }

        // Lab component — Plan 9 Issue 3 paterni: GetLabIntervals plural foreach
        if (section.LabHours > 0)
        {
            try
            {
                var labIntervals = ctx.Variables.GetLabIntervals(section.Id);
                foreach (var iv in labIntervals)
                    BanForInterval(ctx, section.Id, iv, forbiddenIndices);
            }
            catch (KeyNotFoundException)
            {
                // Lab interval kayıtlı değil — sessizce atla.
            }
        }
    }

    private void BanForInterval(
        EncoderContext ctx, int sectionId, IntervalVar? iv, IReadOnlyList<int> forbiddenIndices)
    {
        if (iv == null) return;
        foreach (var slotIdx in forbiddenIndices)
        {
            var literal = ctx.Assumptions.Register(Code,
                primarySectionId: sectionId, secondarySectionId: slotIdx);
            ctx.Model.Add(iv.StartExpr() != slotIdx).OnlyEnforceIf(literal);
        }
    }
}
