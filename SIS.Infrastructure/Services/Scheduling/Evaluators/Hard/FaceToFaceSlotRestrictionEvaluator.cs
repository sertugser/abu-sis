using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

/// <summary>
/// Slot AllowedMode ile TeachingMethod uyumluluğunu hard constraint olarak korur.
/// İki yönlü kontrol:
///   • FaceToFace ders + OnlineOnly slot = ihlal
///   • Online ders + FaceToFaceOnly slot = ihlal
/// Hybrid ders her slota girebilir (esnek).
/// Both slot her ders tipine açık.
/// Single source of truth: TimeSlot.AllowedMode + TeachingMethodPolicy.
/// </summary>
public sealed class FaceToFaceSlotRestrictionEvaluator : IConstraintEvaluator
{
    public string Code => "H17";
    public string Name => "Slot Modu Koruması";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;

        // Hybrid her slota girebilir.
        if (c.TeachingMethod == TeachingMethod.Hybrid)
            return ConstraintResult.Ok();

        var slotModes = context.Snapshot.SlotAllowedModes;
        if (slotModes.Count == 0)
            return ConstraintResult.Ok(); // slot metadata yoksa bu evaluator bypass edilir

        var conflicts = new List<SchedulingConflictDto>();

        foreach (var block in c.TimeBlocks)
        {
            if (!slotModes.TryGetValue((block.Day, block.StartTime), out var mode))
                continue;

            // F2F ders + OnlineOnly slot = ihlal
            if (c.TeachingMethod == TeachingMethod.FaceToFace
                && mode == TimeSlotAllowedMode.OnlineOnly)
            {
                conflicts.Add(SchedulingHelpers.BuildConflict(
                    c,
                    "OnlineOnlySlotConflict",
                    "Bu slot (Online-Only) yalnızca online/hybrid dersler için ayrılmış. Yüz-yüze ders atanamaz.",
                    block,
                    null));
            }

            // Online ders + FaceToFaceOnly slot = ihlal (önceden eksikti — sessiz patlıyordu)
            if (c.TeachingMethod == TeachingMethod.Online
                && mode == TimeSlotAllowedMode.FaceToFaceOnly)
            {
                conflicts.Add(SchedulingHelpers.BuildConflict(
                    c,
                    "FaceToFaceOnlySlotConflict",
                    "Bu slot (Yüz-Yüze) yalnızca yüz-yüze/hybrid dersler için ayrılmış. Online ders atanamaz.",
                    block,
                    null));
            }
        }

        return conflicts.Count > 0
            ? ConstraintResult.WithConflicts(conflicts)
            : ConstraintResult.Ok();
    }
}
