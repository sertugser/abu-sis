using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.17 — H17 FaceToFace/OnlineOnly Slot Koruması.
/// V1 <c>FaceToFaceSlotRestrictionEvaluator</c> ile V2 <c>FaceToFaceSlotEncoder</c> birebir aynı
/// semantik: TimeSlot.AllowedMode'a bakar. Drift yok. Port: V1 mantığı RuntimeBlock'lar üzerinde.
///
/// Hybrid her slota girebilir. Slot metadata yoksa skip. Aksi halde:
///   - F2F ders + OnlineOnly slot → ihlal
///   - Online ders + FaceToFaceOnly slot → ihlal
/// </summary>
public sealed class H17_FaceToFaceSlotRule : IRulePredicate
{
    public string Code => "H17";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;

        if (c.TeachingMethod == TeachingMethod.Hybrid)
            return Array.Empty<RuleViolation>();

        var slotModes = ctx.Snapshot.SlotAllowedModes;
        if (slotModes.Count == 0)
            return Array.Empty<RuleViolation>();

        var violations = new List<RuleViolation>();

        foreach (var block in c.Blocks)
        {
            if (!slotModes.TryGetValue((block.Day, block.StartTime), out var mode))
                continue;

            if (c.TeachingMethod == TeachingMethod.FaceToFace && mode == TimeSlotAllowedMode.OnlineOnly)
            {
                violations.Add(new RuleViolation(
                    Code: Code,
                    ConflictType: "OnlineOnlySlotConflict",
                    Message: "Bu slot (Online-Only) yalnızca online/hybrid dersler için ayrılmış. Yüz-yüze ders atanamaz.",
                    SectionId: c.SectionId,
                    Day: block.Day,
                    StartTime: block.StartTime,
                    EndTime: block.EndTime));
            }
            else if (c.TeachingMethod == TeachingMethod.Online && mode == TimeSlotAllowedMode.FaceToFaceOnly)
            {
                violations.Add(new RuleViolation(
                    Code: Code,
                    ConflictType: "FaceToFaceOnlySlotConflict",
                    Message: "Bu slot (Yüz-Yüze) yalnızca yüz-yüze/hybrid dersler için ayrılmış. Online ders atanamaz.",
                    SectionId: c.SectionId,
                    Day: block.Day,
                    StartTime: block.StartTime,
                    EndTime: block.EndTime));
            }
        }

        return violations;
    }
}
