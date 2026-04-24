using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

/// <summary>
/// Yüz-yüze dersler yalnızca AllowedMode=Both veya FaceToFaceOnly olan slotlara yerleşebilir.
/// OnlineOnly damgalı bir slotta yüz-yüze ders olamaz. Hybrid dersler esnek sayılır (her slota girebilir).
/// Online dersler de her slota girebilir (Online-only kısıtı yok).
/// Source of truth: TimeSlot.AllowedMode (admin UI'dan değiştirilebilir).
/// </summary>
public sealed class FaceToFaceSlotRestrictionEvaluator : IConstraintEvaluator
{
    public string Code => "H17";
    public string Name => "Online-Only Slot Koruması";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;

        // Hybrid ve Online dersler bu kuraldan muaf (esnek — her slota girebilir)
        if (c.TeachingMethod != TeachingMethod.FaceToFace)
            return ConstraintResult.Ok();

        var slotModes = context.Snapshot.SlotAllowedModes;
        if (slotModes.Count == 0)
            return ConstraintResult.Ok(); // slot metadata yoksa bu evaluator bypass edilir

        var conflicts = new List<SchedulingConflictDto>();

        foreach (var block in c.TimeBlocks)
        {
            if (!slotModes.TryGetValue((block.Day, block.StartTime), out var mode))
                continue; // slot lookup bulunamazsa sessiz geç (diğer evaluator'lar zaten tutar)

            if (mode == TimeSlotAllowedMode.OnlineOnly)
            {
                conflicts.Add(SchedulingHelpers.BuildConflict(
                    c,
                    "OnlineOnlySlotConflict",
                    $"Bu slot (Online-Only) yalnızca online/hybrid dersler için ayrılmış. Yüz-yüze ders atanamaz.",
                    block,
                    null));
            }
        }

        return conflicts.Count > 0
            ? ConstraintResult.WithConflicts(conflicts)
            : ConstraintResult.Ok();
    }
}
