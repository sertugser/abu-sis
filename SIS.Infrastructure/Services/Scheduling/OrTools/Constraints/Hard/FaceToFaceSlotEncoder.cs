using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H17 — Face-to-Face Slot Restriction. Yüz-yüze (FaceToFace) section'lar OnlineOnly slot kullanamaz.
/// Online ve Hybrid section'lar OnlineOnly slot kullanabilir (kısıt yok).
///
/// CP-SAT model: TimeSlots içinde AllowedMode == OnlineOnly olanların indeksleri belirlenir.
/// FaceToFace section'lar için lecture/lab interval start indeksi bu slotlara eşit olamaz.
/// Pattern matches H13 LunchBreakEncoder (slot domain filtering).
/// </summary>
public sealed class FaceToFaceSlotEncoder : IConstraintEncoder
{
    public string Code => "H17";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        // Find OnlineOnly slot indices in the term grid
        var forbiddenSlotIndices = new List<int>();
        for (int i = 0; i < ctx.Request.TimeSlots.Count; i++)
        {
            if (ctx.Request.TimeSlots[i].AllowedMode == TimeSlotAllowedMode.OnlineOnly)
                forbiddenSlotIndices.Add(i);
        }

        if (forbiddenSlotIndices.Count == 0)
            return;

        // For each FaceToFace section, forbid lecture/lab interval start at OnlineOnly slot indices
        foreach (var section in ctx.Request.Sections)
        {
            if (section.TeachingMethod != TeachingMethod.FaceToFace)
                continue;

            // Lecture component
            if (section.TheoryHours > 0)
            {
                var iv = ctx.Variables.GetLectureInterval(section.Id);
                if (iv != null)
                {
                    foreach (var slotIdx in forbiddenSlotIndices)
                    {
                        var literal = ctx.Assumptions.Register(Code, primarySectionId: section.Id, secondarySectionId: slotIdx);
                        ctx.Model.Add(iv.StartExpr() != slotIdx).OnlyEnforceIf(literal);
                    }
                }
            }

            // Lab component
            if (section.LabHours > 0)
            {
                var iv = ctx.Variables.GetLabInterval(section.Id);
                if (iv != null)
                {
                    foreach (var slotIdx in forbiddenSlotIndices)
                    {
                        var literal = ctx.Assumptions.Register(Code, primarySectionId: section.Id, secondarySectionId: slotIdx);
                        ctx.Model.Add(iv.StartExpr() != slotIdx).OnlyEnforceIf(literal);
                    }
                }
            }
        }
    }
}
