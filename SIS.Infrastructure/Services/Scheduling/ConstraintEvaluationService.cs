using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Infrastructure.Services.Scheduling;

internal sealed class ConstraintEvaluationService
{
    private readonly IEnumerable<IConstraintEvaluator> _evaluators;

    public ConstraintEvaluationService(IEnumerable<IConstraintEvaluator> evaluators)
    {
        _evaluators = evaluators;
    }

    /// <summary>
    /// Evaluate a single candidate section against term snapshot.
    /// </summary>
    public (List<SchedulingConflictDto> Conflicts, List<string> Notes, double SoftPenalty) EvaluateCandidate(
        SectionSnapshot candidate, TermSnapshot snapshot, SchedulingConstraintConfig config)
    {
        var context = new SchedulingConstraintContext
        {
            Candidate = candidate,
            Snapshot = snapshot,
            Config = config,
        };

        var allConflicts = new List<SchedulingConflictDto>();
        var allNotes = new List<string>();
        double totalPenalty = 0;

        foreach (var evaluator in _evaluators)
        {
            var result = evaluator.Evaluate(context);
            allConflicts.AddRange(result.Conflicts);
            allNotes.AddRange(result.Notes);
            totalPenalty += result.SoftPenalty;
        }

        return (allConflicts, allNotes, totalPenalty);
    }

    /// <summary>
    /// Evaluate all existing sections in a term snapshot (for validate-existing).
    /// </summary>
    public (List<SchedulingConflictDto> Conflicts, List<string> Notes) EvaluateAllExisting(
        TermSnapshot snapshot, SchedulingConstraintConfig config)
    {
        var allConflicts = new List<SchedulingConflictDto>();
        var allNotes = new List<string>();
        var seen = new HashSet<string>(); // dedup bidirectional conflicts

        foreach (var section in snapshot.Sections)
        {
            // Build a snapshot that excludes the current section (evaluators compare candidate vs snapshot)
            var filteredSnapshot = new TermSnapshot
            {
                TermId = snapshot.TermId,
                TermName = snapshot.TermName,
                ScopeName = snapshot.ScopeName,
                Sections = snapshot.Sections.Where(s => s.SectionId != section.SectionId).ToList(),
                ClassroomAssignments = snapshot.ClassroomAssignments,
                DeptFacultyMap = snapshot.DeptFacultyMap,
                Corequisites = snapshot.Corequisites,
                CoEnrollmentPairs = snapshot.CoEnrollmentPairs,
                CourseSuggestedSemestersByDepartment = snapshot.CourseSuggestedSemestersByDepartment,
                CourseAccessibleDepartments = snapshot.CourseAccessibleDepartments,
            };

            var context = new SchedulingConstraintContext
            {
                Candidate = section,
                Snapshot = filteredSnapshot,
                Config = config,
            };

            foreach (var evaluator in _evaluators)
            {
                var result = evaluator.Evaluate(context);
                foreach (var conflict in result.Conflicts)
                {
                    // Deduplicate bidirectional conflicts (A conflicts with B = B conflicts with A)
                    var key = BuildConflictKey(conflict);
                    if (seen.Add(key))
                        allConflicts.Add(conflict);
                }
                allNotes.AddRange(result.Notes);
            }
        }

        return (allConflicts, allNotes);
    }

    /// <summary>
    /// Calculate soft penalty score only (for auto-scheduling scoring).
    /// </summary>
    public double CalculatePenalty(
        SectionSnapshot candidate, TermSnapshot snapshot, SchedulingConstraintConfig config)
    {
        var context = new SchedulingConstraintContext
        {
            Candidate = candidate,
            Snapshot = snapshot,
            Config = config,
        };

        double totalPenalty = 0;
        foreach (var evaluator in _evaluators.Where(e => e.Category == ConstraintCategory.Soft))
        {
            totalPenalty += evaluator.Evaluate(context).SoftPenalty;
        }
        return totalPenalty;
    }

    private static string BuildConflictKey(SchedulingConflictDto c)
    {
        var ids = new[] { c.SectionId, c.RelatedSectionId ?? 0 };
        Array.Sort(ids);
        return $"{c.ConflictType}:{ids[0]}:{ids[1]}:{c.Day}:{c.StartTime}";
    }
}
