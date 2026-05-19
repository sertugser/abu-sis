namespace SIS.Infrastructure.Services.Scheduling.OrTools.Model;

/// <summary>
/// Plan 7b — Lab time-split helper. v1 <c>CandidateGenerator.SplitLabIntoBlocks</c> portu.
/// totalLabHours saatlik labı maxBlockHours'lerden parçalara böler:
///   - totalLabHours ≤ 0 → []
///   - maxBlockHours ≤ 0 → [] (defensive)
///   - totalLabHours ≤ maxBlockHours → [totalLabHours] (tek blok)
///   - aksi halde N tam maxBlockHours blok + son remainder (varsa)
///
/// Örnek: <c>SplitLabIntoBlocks(4, 2)</c> → <c>[2, 2]</c>; <c>SplitLabIntoBlocks(5, 2)</c> → <c>[2, 2, 1]</c>.
/// Bloklar farklı günlere yerleştirilir (v1: BuildMultiLabCombinations gün-uniqueness; v2: AddAllDifferent over day vars).
/// </summary>
public static class LabBlockSplitter
{
    public static IReadOnlyList<int> SplitLabIntoBlocks(int totalLabHours, int maxBlockHours)
    {
        if (totalLabHours <= 0 || maxBlockHours <= 0) return [];
        if (totalLabHours <= maxBlockHours) return new[] { totalLabHours };

        var blocks = new List<int>();
        var remaining = totalLabHours;
        while (remaining > maxBlockHours)
        {
            blocks.Add(maxBlockHours);
            remaining -= maxBlockHours;
        }
        if (remaining > 0) blocks.Add(remaining);
        return blocks;
    }
}
