using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Soft;

/// <summary>
/// S21 Online Slot Tercihi (Soft). Online ders OnlineOnly damgalı slot dışında bir yere
/// yerleşirse her blok için sabit penalty.
///
/// Amaç: Online dersleri OnlineOnly slot'a (akşam/hafta sonu vb.) yerleştirmeyi TEŞVİK etmek
/// AMA gerekirse Both slot'a düşmesine izin vermek. H17 hard kuralından farkı: H17 FaceToFace
/// dersi OnlineOnly slot'a girmekten ENGELLER (hard). S21 ise tersi yönde TERCIH eder.
///
/// Kuraldan muaf:
///   - Hybrid dersler (her slot tipi serbest)
///   - FaceToFace dersler (zaten Online slot'a girmemeli — H17 koruyor)
/// Sadece Online dersler kapsamda.
/// </summary>
public sealed class S21_OnlineSlotPreferenceRule : ISoftRulePredicate
{
    private const double PenaltyPerBlock = 5.0;

    public string Code => "S21";
    public RuleCategory DefaultCategory => RuleCategory.Soft;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx) => Array.Empty<RuleViolation>();

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var c = ctx.Candidate;
        if (c.TeachingMethod != TeachingMethod.Online) return SoftRuleResult.Zero;
        if (c.Blocks.Count == 0) return SoftRuleResult.Zero;

        var slotModes = ctx.Snapshot.SlotAllowedModes;
        if (slotModes.Count == 0) return SoftRuleResult.Zero;

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var block in c.Blocks)
        {
            if (!slotModes.TryGetValue((block.Day, block.StartTime), out var mode))
                continue;
            // OnlineOnly slot tercih edilen yer; Both slot ise penalty.
            if (mode == TimeSlotAllowedMode.OnlineOnly) continue;

            totalPenalty += PenaltyPerBlock;
            if (notes.Count < 3)
                notes.Add($"S21: {c.CourseCode} (Online) OnlineOnly slot dışına yerleşti ({block.StartTime:HH\\:mm}-{block.EndTime:HH\\:mm}).");
        }

        return totalPenalty > 0
            ? new SoftRuleResult(totalPenalty, notes.Count, 1, notes)
            : SoftRuleResult.Zero;
    }
}
