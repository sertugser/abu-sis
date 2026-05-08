using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Model;

/// <summary>
/// Plan 7 — Slot block validity utility. Computes valid start indices for a section
/// of given duration given the term's TimeSlots.
///
/// A start index <c>i</c> is valid iff:
///   1. <c>i + duration &lt;= timeSlots.Count</c> (block fits in slot grid)
///   2. All slots <c>[i, i+duration)</c> share the same <c>DayOfWeek</c>
///   3. For every consecutive pair, <c>gap = next.StartTime - prev.EndTime ∈ [0, 10] dk</c>
///
/// 10-dk threshold matches v1 <c>CandidateGenerator.cs:582</c> rule. H15 break duration
/// (5dk, MaxConsecutiveHours rule) is a different, stricter threshold.
/// </summary>
public static class SlotBlockValidator
{
    private const int MaxGapMinutes = 10;

    public static IReadOnlyList<int> ComputeValidBlockStarts(
        IReadOnlyList<TimeSlotInput> timeSlots,
        int duration)
    {
        if (duration <= 0 || timeSlots.Count == 0) return [];
        if (duration > timeSlots.Count) return [];

        var validStarts = new List<int>();
        for (int i = 0; i + duration <= timeSlots.Count; i++)
        {
            if (IsValidBlock(timeSlots, i, duration))
                validStarts.Add(i);
        }
        return validStarts;
    }

    private static bool IsValidBlock(IReadOnlyList<TimeSlotInput> timeSlots, int start, int duration)
    {
        var firstDay = timeSlots[start].DayOfWeek;
        for (int k = 1; k < duration; k++)
        {
            var prev = timeSlots[start + k - 1];
            var cur = timeSlots[start + k];

            // Same day check
            if (cur.DayOfWeek != firstDay) return false;

            // Gap check
            var gapMinutes = (cur.StartTime - prev.EndTime).TotalMinutes;
            if (gapMinutes < 0 || gapMinutes > MaxGapMinutes) return false;
        }
        return true;
    }
}
