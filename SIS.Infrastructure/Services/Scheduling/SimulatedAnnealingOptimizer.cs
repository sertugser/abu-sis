using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed record SAResult(
    int Iterations,
    double InitialPenalty,
    double FinalPenalty,
    double ElapsedSeconds);

public sealed class SimulatedAnnealingOptimizer
{
    private readonly CandidateGenerator _candidateGen;
    private readonly ConstraintEvaluationService _constraintService;

    public SimulatedAnnealingOptimizer(
        CandidateGenerator candidateGen,
        ConstraintEvaluationService constraintService)
    {
        _candidateGen = candidateGen;
        _constraintService = constraintService;
    }

    public SAResult Optimize(
        MutableScheduleState state,
        List<Classroom> classrooms,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        SchedulingConstraintConfig config,
        ConstraintState constraintState,
        TermSnapshot baseSnapshot,
        HashSet<int> facultyClassroomIds,
        int splitThreshold,
        int seed,
        int maxIterations,
        int timeoutSeconds,
        int stagnationLimit,
        double initialTemperature,
        int relocateWeight,
        int swapWeight,
        int chainWeight)
    {
        var random = new Random(seed);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Get non-pinned section ids
        var nonPinnedIds = state.GetAssignedNonPinnedSectionIds();
        if (nonPinnedIds.Count < 2)
        {
            return new SAResult(0, 0, 0, stopwatch.Elapsed.TotalSeconds);
        }

        // Calculate initial penalty with delta-caching
        var penaltyCache = new Dictionary<int, double>();
        var termSnapshot = state.ToTermSnapshot(baseSnapshot);
        foreach (var sectionId in state.AllPlacements.Keys)
        {
            var sectionSnap = state.ToSectionSnapshot(sectionId);
            var filteredSnapshot = BuildFilteredSnapshot(termSnapshot, sectionId);
            var penalty = _constraintService.CalculatePenalty(sectionSnap, filteredSnapshot, config, constraintState);
            penaltyCache[sectionId] = penalty;
        }

        var currentPenalty = penaltyCache.Values.Sum();
        var initialPenalty = currentPenalty;

        var bestState = state.Clone();
        var bestPenalty = currentPenalty;

        var coolingRate = 1.0 - (1.0 / (maxIterations * 0.3));
        var temperature = initialTemperature;
        var totalWeight = relocateWeight + swapWeight + chainWeight;
        var stagnationCounter = 0;
        var iterations = 0;

        for (iterations = 0; iterations < maxIterations; iterations++)
        {
            // Stop conditions
            if (stopwatch.Elapsed.TotalSeconds > timeoutSeconds)
                break;
            if (stagnationCounter >= stagnationLimit)
                break;

            // Select move type
            var roll = random.Next(totalWeight);
            MoveType moveType;
            if (roll < relocateWeight)
                moveType = MoveType.Relocate;
            else if (roll < relocateWeight + swapWeight)
                moveType = MoveType.Swap;
            else
                moveType = MoveType.Chain;

            // Execute move
            var (affectedIds, rollback, success) = moveType switch
            {
                MoveType.Relocate => TryRelocate(state, nonPinnedIds, classrooms, timeSlotsByDay, splitThreshold, facultyClassroomIds, random),
                MoveType.Swap => TrySwap(state, nonPinnedIds, random),
                MoveType.Chain => TryChain(state, nonPinnedIds, random),
                _ => (Array.Empty<int>(), (Action?)null, false)
            };

            if (!success || rollback == null)
            {
                stagnationCounter++;
                temperature *= coolingRate;
                continue;
            }

            // Build snapshot and check hard constraints
            termSnapshot = state.ToTermSnapshot(baseSnapshot);
            var hardViolation = false;
            foreach (var sectionId in affectedIds)
            {
                if (!state.IsAssigned(sectionId)) continue;
                var sectionSnap = state.ToSectionSnapshot(sectionId);
                var filteredSnapshot = BuildFilteredSnapshot(termSnapshot, sectionId);
                var (conflicts, _, _) = _constraintService.EvaluateCandidate(sectionSnap, filteredSnapshot, config, constraintState);
                if (conflicts.Count > 0)
                {
                    hardViolation = true;
                    break;
                }
            }

            if (hardViolation)
            {
                rollback();
                stagnationCounter++;
                temperature *= coolingRate;
                continue;
            }

            // Delta penalty calculation
            var oldAffectedPenalty = 0.0;
            foreach (var sectionId in affectedIds)
            {
                if (penaltyCache.TryGetValue(sectionId, out var cached))
                    oldAffectedPenalty += cached;
            }

            var newAffectedPenalty = 0.0;
            var newPenalties = new Dictionary<int, double>();
            foreach (var sectionId in affectedIds)
            {
                if (!state.IsAssigned(sectionId)) continue;
                var sectionSnap = state.ToSectionSnapshot(sectionId);
                var filteredSnapshot = BuildFilteredSnapshot(termSnapshot, sectionId);
                var penalty = _constraintService.CalculatePenalty(sectionSnap, filteredSnapshot, config, constraintState);
                newPenalties[sectionId] = penalty;
                newAffectedPenalty += penalty;
            }

            var newTotalPenalty = currentPenalty - oldAffectedPenalty + newAffectedPenalty;
            var delta = newTotalPenalty - currentPenalty;

            // Accept/reject
            if (delta < 0)
            {
                // Improvement — accept
                currentPenalty = newTotalPenalty;
                foreach (var (id, p) in newPenalties)
                    penaltyCache[id] = p;
                stagnationCounter = 0;

                if (currentPenalty < bestPenalty)
                {
                    bestPenalty = currentPenalty;
                    bestState = state.Clone();
                }
            }
            else
            {
                // Worsening — accept probabilistically
                var acceptProbability = Math.Exp(-delta / temperature);
                if (random.NextDouble() < acceptProbability)
                {
                    currentPenalty = newTotalPenalty;
                    foreach (var (id, p) in newPenalties)
                        penaltyCache[id] = p;
                }
                else
                {
                    rollback();
                }
                stagnationCounter++;
            }

            temperature *= coolingRate;
        }

        // Restore best state into working state
        RestoreState(state, bestState);

        stopwatch.Stop();
        return new SAResult(iterations, initialPenalty, bestPenalty, stopwatch.Elapsed.TotalSeconds);
    }

    private (int[] AffectedIds, Action? Rollback, bool Success) TryRelocate(
        MutableScheduleState state,
        List<int> nonPinnedIds,
        List<Classroom> classrooms,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        int splitThreshold,
        HashSet<int> facultyClassroomIds,
        Random random)
    {
        var sectionId = nonPinnedIds[random.Next(nonPinnedIds.Count)];
        var placement = state.GetPlacement(sectionId);
        if (placement == null)
            return (Array.Empty<int>(), null, false);

        var oldBlocks = placement.Blocks.ToList();
        var snapshot = placement.Snapshot;

        state.Unassign(sectionId);

        var excludeBlocks = new HashSet<BlockAssignment>(oldBlocks);
        var candidates = _candidateGen.Generate(snapshot, state, classrooms, timeSlotsByDay, splitThreshold, facultyClassroomIds, excludeBlocks);

        if (candidates.Count == 0)
        {
            // No candidates — restore original
            state.Assign(sectionId, snapshot, oldBlocks);
            return (Array.Empty<int>(), null, false);
        }

        var chosen = candidates[random.Next(candidates.Count)];
        var newBlocks = new List<BlockAssignment>();
        if (chosen.TheoryBlock != null) newBlocks.Add(chosen.TheoryBlock);
        newBlocks.AddRange(chosen.LabBlocks);

        state.Assign(sectionId, snapshot, newBlocks);

        Action rollback = () =>
        {
            state.Unassign(sectionId);
            state.Assign(sectionId, snapshot, oldBlocks);
        };

        return (new[] { sectionId }, rollback, true);
    }

    private (int[] AffectedIds, Action? Rollback, bool Success) TrySwap(
        MutableScheduleState state,
        List<int> nonPinnedIds,
        Random random)
    {
        if (nonPinnedIds.Count < 2)
            return (Array.Empty<int>(), null, false);

        var idx1 = random.Next(nonPinnedIds.Count);
        int idx2;
        do { idx2 = random.Next(nonPinnedIds.Count); } while (idx2 == idx1);

        var id1 = nonPinnedIds[idx1];
        var id2 = nonPinnedIds[idx2];

        var p1 = state.GetPlacement(id1);
        var p2 = state.GetPlacement(id2);
        if (p1 == null || p2 == null)
            return (Array.Empty<int>(), null, false);

        var blocks1 = p1.Blocks.ToList();
        var blocks2 = p2.Blocks.ToList();
        var snap1 = p1.Snapshot;
        var snap2 = p2.Snapshot;

        state.Unassign(id1);
        state.Unassign(id2);

        // Swap: section1 gets section2's blocks, section2 gets section1's blocks
        state.Assign(id1, snap1, blocks2);
        state.Assign(id2, snap2, blocks1);

        Action rollback = () =>
        {
            state.Unassign(id1);
            state.Unassign(id2);
            state.Assign(id1, snap1, blocks1);
            state.Assign(id2, snap2, blocks2);
        };

        return (new[] { id1, id2 }, rollback, true);
    }

    private (int[] AffectedIds, Action? Rollback, bool Success) TryChain(
        MutableScheduleState state,
        List<int> nonPinnedIds,
        Random random)
    {
        if (nonPinnedIds.Count < 3)
            return TrySwap(state, nonPinnedIds, random); // Fallback to swap

        var indices = new HashSet<int>();
        while (indices.Count < 3)
            indices.Add(random.Next(nonPinnedIds.Count));

        var idxList = indices.ToList();
        var idA = nonPinnedIds[idxList[0]];
        var idB = nonPinnedIds[idxList[1]];
        var idC = nonPinnedIds[idxList[2]];

        var pA = state.GetPlacement(idA);
        var pB = state.GetPlacement(idB);
        var pC = state.GetPlacement(idC);
        if (pA == null || pB == null || pC == null)
            return (Array.Empty<int>(), null, false);

        var blocksA = pA.Blocks.ToList();
        var blocksB = pB.Blocks.ToList();
        var blocksC = pC.Blocks.ToList();
        var snapA = pA.Snapshot;
        var snapB = pB.Snapshot;
        var snapC = pC.Snapshot;

        state.Unassign(idA);
        state.Unassign(idB);
        state.Unassign(idC);

        // Chain: A gets B's blocks, B gets C's blocks, C gets A's blocks
        state.Assign(idA, snapA, blocksB);
        state.Assign(idB, snapB, blocksC);
        state.Assign(idC, snapC, blocksA);

        Action rollback = () =>
        {
            state.Unassign(idA);
            state.Unassign(idB);
            state.Unassign(idC);
            state.Assign(idA, snapA, blocksA);
            state.Assign(idB, snapB, blocksB);
            state.Assign(idC, snapC, blocksC);
        };

        return (new[] { idA, idB, idC }, rollback, true);
    }

    private static TermSnapshot BuildFilteredSnapshot(TermSnapshot snapshot, int excludeSectionId)
    {
        return new TermSnapshot
        {
            TermId = snapshot.TermId,
            TermName = snapshot.TermName,
            ScopeName = snapshot.ScopeName,
            Sections = snapshot.Sections.Where(s => s.SectionId != excludeSectionId).ToList(),
            ClassroomAssignments = snapshot.ClassroomAssignments,
            DeptFacultyMap = snapshot.DeptFacultyMap,
            Corequisites = snapshot.Corequisites,
            CoEnrollmentPairs = snapshot.CoEnrollmentPairs,
            CourseSuggestedSemestersByDepartment = snapshot.CourseSuggestedSemestersByDepartment,
            CourseAccessibleDepartments = snapshot.CourseAccessibleDepartments
        };
    }

    private static void RestoreState(MutableScheduleState working, MutableScheduleState best)
    {
        // Unassign all non-pinned from working
        var nonPinnedWorking = working.GetAssignedNonPinnedSectionIds();
        foreach (var id in nonPinnedWorking)
            working.Unassign(id);

        // Copy all non-pinned from best into working
        var nonPinnedBest = best.GetAssignedNonPinnedSectionIds();
        foreach (var id in nonPinnedBest)
        {
            var placement = best.GetPlacement(id);
            if (placement != null)
                working.Assign(id, placement.Snapshot, placement.Blocks.ToList());
        }
    }

    private enum MoveType { Relocate, Swap, Chain }
}
