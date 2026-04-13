namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Atama denemelerinde karsilasilan conflict'leri izler.
/// Variable ordering (fail-first) ve value ordering (least-conflicting) icin kullanilir.
/// </summary>
public sealed class ConflictStatistics
{
    private readonly Dictionary<(int SectionId, string ConflictType), int> _sectionConflicts = new();
    private readonly Dictionary<(int SectionId, DayOfWeek Day, TimeOnly Start), int> _slotConflicts = new();
    private readonly Dictionary<int, int> _totalConflicts = new();

    public void RecordConflict(int sectionId, string conflictType, DayOfWeek? day, TimeOnly? startTime)
    {
        var key1 = (sectionId, conflictType);
        _sectionConflicts[key1] = _sectionConflicts.GetValueOrDefault(key1) + 1;
        _totalConflicts[sectionId] = _totalConflicts.GetValueOrDefault(sectionId) + 1;

        if (day.HasValue && startTime.HasValue)
        {
            var key2 = (sectionId, day.Value, startTime.Value);
            _slotConflicts[key2] = _slotConflicts.GetValueOrDefault(key2) + 1;
        }
    }

    public int GetFailCount(int sectionId) =>
        _totalConflicts.GetValueOrDefault(sectionId);

    public List<BlockCombination> OrderByLeastConflicting(
        int sectionId, List<BlockCombination> candidates)
    {
        return candidates
            .OrderBy(c => GetComboConflictScore(sectionId, c))
            .ToList();
    }

    private int GetComboConflictScore(int sectionId, BlockCombination combo)
    {
        var score = 0;
        if (combo.TheoryBlock != null)
            score += _slotConflicts.GetValueOrDefault((sectionId, combo.TheoryBlock.Day, combo.TheoryBlock.StartTime));
        foreach (var lab in combo.LabBlocks)
            score += _slotConflicts.GetValueOrDefault((sectionId, lab.Day, lab.StartTime));
        return score;
    }
}
