using SIS.Infrastructure.Services;

namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Her section icin atanabilir BlockCombination listesini (domain) tutar.
/// Forward checking: bir atama sonrasi etkilenen domain'lerden gecersiz adaylari siler.
/// </summary>
public sealed class DomainStore
{
    private readonly Dictionary<int, List<BlockCombination>> _domains = new();
    private readonly Dictionary<int, SectionSnapshot> _snapshots = new();
    private readonly Stack<DomainPruneRecord> _pruneHistory = new();

    public void InitDomain(int sectionId, SectionSnapshot snapshot, List<BlockCombination> candidates)
    {
        _domains[sectionId] = new List<BlockCombination>(candidates);
        _snapshots[sectionId] = snapshot;
    }

    public int DomainSize(int sectionId) =>
        _domains.TryGetValue(sectionId, out var d) ? d.Count : 0;

    public bool IsWipedOut(int sectionId) => DomainSize(sectionId) == 0;

    public List<BlockCombination> GetCandidates(int sectionId) =>
        _domains.TryGetValue(sectionId, out var d) ? d : new();

    public List<int> GetUnassignedByMrv(HashSet<int> assignedIds)
    {
        return _domains
            .Where(kvp => !assignedIds.Contains(kvp.Key))
            .OrderBy(kvp => kvp.Value.Count)
            .ThenByDescending(kvp => GetDegree(kvp.Key, assignedIds))
            .Select(kvp => kvp.Key)
            .ToList();
    }

    public ForwardCheckResult ForwardCheck(
        int assignedSectionId,
        List<BlockAssignment> assignedBlocks,
        SectionSnapshot assignedSnapshot,
        HashSet<int> assignedIds,
        Func<int, BlockCombination, bool> isConflicting)
    {
        var pruned = new Dictionary<int, List<BlockCombination>>();
        var wipedOut = false;

        foreach (var (sectionId, candidates) in _domains)
        {
            if (assignedIds.Contains(sectionId))
                continue;

            var toRemove = new List<BlockCombination>();
            foreach (var combo in candidates)
            {
                if (isConflicting(sectionId, combo))
                    toRemove.Add(combo);
            }

            if (toRemove.Count > 0)
            {
                foreach (var item in toRemove)
                    candidates.Remove(item);
                pruned[sectionId] = toRemove;

                if (candidates.Count == 0)
                {
                    wipedOut = true;
                    break;
                }
            }
        }

        _pruneHistory.Push(new DomainPruneRecord(assignedSectionId, pruned));
        return new ForwardCheckResult(WipedOut: wipedOut, PrunedCount: pruned.Values.Sum(v => v.Count));
    }

    public void UndoLastPrune()
    {
        if (_pruneHistory.Count == 0) return;
        var record = _pruneHistory.Pop();
        foreach (var (sectionId, removed) in record.PrunedDomains)
        {
            if (_domains.TryGetValue(sectionId, out var domain))
                domain.AddRange(removed);
        }
    }

    private int GetDegree(int sectionId, HashSet<int> assignedIds)
    {
        if (!_snapshots.TryGetValue(sectionId, out var snap))
            return 0;

        var count = 0;
        foreach (var (otherId, otherSnap) in _snapshots)
        {
            if (otherId == sectionId || assignedIds.Contains(otherId))
                continue;
            if (snap.InstructorId.HasValue && snap.InstructorId == otherSnap.InstructorId)
                count++;
            else if (snap.DepartmentId.HasValue && snap.DepartmentId == otherSnap.DepartmentId
                     && snap.SuggestedSemester.HasValue && otherSnap.SuggestedSemester.HasValue
                     && (snap.SuggestedSemester.Value - 1) / 2 == (otherSnap.SuggestedSemester.Value - 1) / 2)
                count++;
        }
        return count;
    }
}

public sealed record DomainPruneRecord(
    int AssignedSectionId,
    Dictionary<int, List<BlockCombination>> PrunedDomains);

public sealed record ForwardCheckResult(
    bool WipedOut,
    int PrunedCount);
