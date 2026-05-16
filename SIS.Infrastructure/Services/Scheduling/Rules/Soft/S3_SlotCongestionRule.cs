using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Soft;

/// <summary>
/// Plan 12 Faz 2.3 — S3 Slot Yığılması (V1 <c>SlotCongestionEvaluator</c> portu).
///
/// V1 davranışı: snapshot.Sections tüm bloklarının (Day, StartTime) çiftlerini topla;
/// candidate'in slot'ları içinde olan ve count &gt; S3MaxSectionsPerSlot olanlar için
/// <c>(count − cap) × 5</c> penalty.
///
/// V1 quirk: candidate snapshot içindeyse blokları iki kez sayılır
/// (existingSlotKeys ∋ candidate slots, sonra candidateSlotKeys concat).
/// </summary>
public sealed class S3_SlotCongestionRule : ISoftRulePredicate
{
    public string Code => "S3";
    public RuleCategory DefaultCategory => RuleCategory.Soft;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx) => Array.Empty<RuleViolation>();

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var config = ctx.Config;

        if (!config.S3Enabled || c.Blocks.Count == 0) return SoftRuleResult.Zero;

        var existingSlotKeys = ctx.Snapshot.Sections
            .SelectMany(s => s.Blocks.Select(b => (b.Day, b.StartTime)))
            .ToList();

        var candidateSlotKeys = c.Blocks
            .Select(b => (b.Day, b.StartTime))
            .ToHashSet();

        var allSlotKeys = existingSlotKeys.Concat(candidateSlotKeys);

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var group in allSlotKeys.GroupBy(x => x))
        {
            var count = group.Count();
            if (count > config.S3MaxSectionsPerSlot && candidateSlotKeys.Contains(group.Key))
            {
                totalPenalty += (count - config.S3MaxSectionsPerSlot) * 5;
                notes.Add($"S3 Uyarı: {DayName(group.Key.Day)} {group.Key.StartTime:HH\\:mm} slotunda {count} section yığılması var (max {config.S3MaxSectionsPerSlot})");
            }
        }

        return totalPenalty > 0
            ? new SoftRuleResult(totalPenalty, notes.Count, 1, notes)
            : SoftRuleResult.Zero;
    }

    private static string DayName(int day) => day switch
    {
        0 => "Pazar", 1 => "Pazartesi", 2 => "Salı", 3 => "Çarşamba",
        4 => "Perşembe", 5 => "Cuma", 6 => "Cumartesi", _ => $"day{day}"
    };
}
