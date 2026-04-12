using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class FailureAnalyzerResult
{
    public int SectionId { get; init; }
    public string CourseCode { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public List<FailureSuggestionDto> Suggestions { get; init; } = new();
}

public sealed class FailureAnalyzer
{
    private const int MaxCandidatesToTest = 5;

    private readonly CandidateGenerator _candidateGen;
    private readonly ConstraintEvaluationService _constraintService;

    public FailureAnalyzer(
        CandidateGenerator candidateGen,
        ConstraintEvaluationService constraintService)
    {
        _candidateGen = candidateGen;
        _constraintService = constraintService;
    }

    public List<FailureAnalyzerResult> Analyze(
        List<int> failedSectionIds,
        Dictionary<int, SectionSnapshot> sectionSnapshots,
        MutableScheduleState state,
        List<Classroom> classrooms,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        SchedulingConstraintConfig config,
        ConstraintState constraintState,
        TermSnapshot baseSnapshot,
        HashSet<int> facultyClassroomIds,
        int splitThreshold)
    {
        var results = new List<FailureAnalyzerResult>();

        foreach (var sectionId in failedSectionIds)
        {
            if (!sectionSnapshots.TryGetValue(sectionId, out var snapshot))
                continue;

            var suggestions = AnalyzeSection(
                snapshot, state, classrooms, timeSlotsByDay,
                config, constraintState, baseSnapshot,
                facultyClassroomIds, splitThreshold);

            // Sort by priority ascending (lowest = cheapest fix first)
            suggestions = suggestions
                .OrderBy(s => s.Priority)
                .ToList();

            var reason = suggestions.Count > 0
                ? suggestions[0].Message
                : "Could not be placed due to scheduling constraints.";

            results.Add(new FailureAnalyzerResult
            {
                SectionId = sectionId,
                CourseCode = snapshot.CourseCode,
                CourseName = snapshot.CourseName,
                Reason = reason,
                Suggestions = suggestions
            });
        }

        return results;
    }

    private List<FailureSuggestionDto> AnalyzeSection(
        SectionSnapshot snapshot,
        MutableScheduleState state,
        List<Classroom> classrooms,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        SchedulingConstraintConfig config,
        ConstraintState constraintState,
        TermSnapshot baseSnapshot,
        HashSet<int> facultyClassroomIds,
        int splitThreshold)
    {
        // Generate candidates without excluding any blocks
        var candidates = _candidateGen.Generate(
            snapshot, state, classrooms, timeSlotsByDay,
            splitThreshold, facultyClassroomIds);

        if (candidates.Count == 0)
        {
            // No candidates at all — diagnose WHY
            return DiagnoseNoCandidates(snapshot, classrooms, facultyClassroomIds);
        }

        // Candidates exist but all fail — test up to MaxCandidatesToTest
        return DiagnoseFailingCandidates(
            snapshot, candidates, state, config, constraintState, baseSnapshot);
    }

    private static List<FailureSuggestionDto> DiagnoseNoCandidates(
        SectionSnapshot snapshot,
        List<Classroom> classrooms,
        HashSet<int> facultyClassroomIds)
    {
        var suggestions = new List<FailureSuggestionDto>();

        // Check theory classroom availability
        if (snapshot.TheoryHours > 0)
        {
            var theoryType = snapshot.TheoryClassroomType ?? ClassroomType.Lecture;
            var hasMatchingTheory = classrooms
                .Any(c => facultyClassroomIds.Contains(c.Id)
                          && c.Type == theoryType
                          && c.Capacity >= snapshot.Capacity);

            if (!hasMatchingTheory)
            {
                // Check if the type exists at all vs capacity issue
                var typeExists = classrooms.Any(c =>
                    facultyClassroomIds.Contains(c.Id) && c.Type == theoryType);

                if (!typeExists)
                {
                    suggestions.Add(new FailureSuggestionDto
                    {
                        ConstraintCode = "ClassroomTypeConflict",
                        Message = $"No {theoryType} classroom is assigned to this faculty. Add a classroom of the required type.",
                        SuggestionType = "ClassroomAdd",
                        Priority = 5
                    });
                }
                else
                {
                    suggestions.Add(new FailureSuggestionDto
                    {
                        ConstraintCode = "CapacityConflict",
                        Message = $"No {theoryType} classroom has sufficient capacity ({snapshot.Capacity} seats) in this faculty. Increase classroom capacity or reduce section size.",
                        SuggestionType = "CapacityIncrease",
                        Priority = 1
                    });
                }
            }
        }

        // Check lab classroom availability
        if (snapshot.LabHours > 0)
        {
            var labType = snapshot.LabClassroomType ?? ClassroomType.Lab;
            var hasMatchingLab = classrooms
                .Any(c => facultyClassroomIds.Contains(c.Id)
                          && c.Type == labType
                          && c.Capacity >= snapshot.Capacity);

            if (!hasMatchingLab)
            {
                var typeExists = classrooms.Any(c =>
                    facultyClassroomIds.Contains(c.Id) && c.Type == labType);

                if (!typeExists)
                {
                    suggestions.Add(new FailureSuggestionDto
                    {
                        ConstraintCode = "ClassroomTypeConflict",
                        Message = $"No {labType} classroom is assigned to this faculty. Add a classroom of the required type.",
                        SuggestionType = "ClassroomAdd",
                        Priority = 5
                    });
                }
                else
                {
                    suggestions.Add(new FailureSuggestionDto
                    {
                        ConstraintCode = "CapacityConflict",
                        Message = $"No {labType} classroom has sufficient capacity ({snapshot.Capacity} seats) in this faculty. Increase classroom capacity or reduce section size.",
                        SuggestionType = "CapacityIncrease",
                        Priority = 1
                    });
                }
            }
        }

        // If theory and lab type both exist with capacity but still no candidates,
        // it must be a time-slot availability issue
        if (suggestions.Count == 0)
        {
            suggestions.Add(new FailureSuggestionDto
            {
                ConstraintCode = "NoAvailableSlot",
                Message = "No available time slots found for this section. Add more time slots or free up existing slots.",
                SuggestionType = "TimeChange",
                Priority = 4
            });
        }

        return suggestions;
    }

    private List<FailureSuggestionDto> DiagnoseFailingCandidates(
        SectionSnapshot snapshot,
        List<BlockCombination> candidates,
        MutableScheduleState state,
        SchedulingConstraintConfig config,
        ConstraintState constraintState,
        TermSnapshot baseSnapshot)
    {
        // Collect all conflict types from testing up to MaxCandidatesToTest candidates
        var conflictTypes = new HashSet<string>();

        var toTest = candidates.Take(MaxCandidatesToTest).ToList();

        foreach (var candidate in toTest)
        {
            var testSnapshot = BuildTestSnapshot(snapshot, candidate, state, baseSnapshot);

            var (conflicts, _, _) = _constraintService.EvaluateCandidate(
                testSnapshot, baseSnapshot, config, constraintState);

            foreach (var conflict in conflicts)
                conflictTypes.Add(conflict.ConflictType);
        }

        // Map conflict types to suggestions, deduplicated by ConstraintCode (keep lowest priority)
        var suggestionMap = new Dictionary<string, FailureSuggestionDto>();

        foreach (var conflictType in conflictTypes)
        {
            var suggestion = MapConflictToSuggestion(conflictType);

            if (!suggestionMap.TryGetValue(suggestion.ConstraintCode, out var existing)
                || suggestion.Priority < existing.Priority)
            {
                suggestionMap[suggestion.ConstraintCode] = suggestion;
            }
        }

        return suggestionMap.Values.ToList();
    }

    private static SectionSnapshot BuildTestSnapshot(
        SectionSnapshot snapshot,
        BlockCombination candidate,
        MutableScheduleState state,
        TermSnapshot baseSnapshot)
    {
        // Build TimeBlocks from the candidate's block assignments
        var timeBlocks = new List<TimeBlock>();
        int? classroomId = null;

        if (candidate.TheoryBlock != null)
        {
            timeBlocks.Add(new TimeBlock
            {
                Day = candidate.TheoryBlock.Day,
                StartTime = candidate.TheoryBlock.StartTime,
                EndTime = candidate.TheoryBlock.EndTime,
                ClassroomId = candidate.TheoryBlock.ClassroomId
            });
            classroomId = candidate.TheoryBlock.ClassroomId;
        }

        if (candidate.LabBlock != null)
        {
            timeBlocks.Add(new TimeBlock
            {
                Day = candidate.LabBlock.Day,
                StartTime = candidate.LabBlock.StartTime,
                EndTime = candidate.LabBlock.EndTime,
                ClassroomId = candidate.LabBlock.ClassroomId
            });
            classroomId ??= candidate.LabBlock.ClassroomId;
        }

        var totalHours = timeBlocks.Sum(b =>
            (int)Math.Ceiling((b.EndTime - b.StartTime).TotalHours));

        return new SectionSnapshot
        {
            SectionId = snapshot.SectionId,
            CourseId = snapshot.CourseId,
            CourseCode = snapshot.CourseCode,
            CourseName = snapshot.CourseName,
            TermId = snapshot.TermId,
            TheoryHours = snapshot.TheoryHours,
            LabHours = snapshot.LabHours,
            Capacity = snapshot.Capacity,
            EnrolledCount = snapshot.EnrolledCount,
            ClassroomId = classroomId,
            ClassroomName = snapshot.ClassroomName,
            ClassroomCapacity = snapshot.ClassroomCapacity,
            ClassroomType = snapshot.ClassroomType,
            TheoryClassroomType = snapshot.TheoryClassroomType,
            LabClassroomType = snapshot.LabClassroomType,
            InstructorId = snapshot.InstructorId,
            InstructorName = snapshot.InstructorName,
            InstructorMaxWeeklyHours = snapshot.InstructorMaxWeeklyHours,
            DepartmentId = snapshot.DepartmentId,
            SuggestedSemester = snapshot.SuggestedSemester,
            SuggestedSemestersByDepartment = snapshot.SuggestedSemestersByDepartment,
            AccessibleDepartmentIds = snapshot.AccessibleDepartmentIds,
            AssignedTimeSlotCount = totalHours,
            TimeBlocks = timeBlocks,
            Notes = snapshot.Notes
        };
    }

    private static FailureSuggestionDto MapConflictToSuggestion(string conflictType)
    {
        return conflictType switch
        {
            "CapacityConflict" => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = "Section capacity exceeds available classroom capacity. Increase classroom capacity or split the section.",
                SuggestionType = "CapacityIncrease",
                Priority = 1
            },
            "InstructorConflict" => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = "Instructor has a scheduling conflict. Reassign the instructor or adjust their other sections.",
                SuggestionType = "InstructorReassign",
                Priority = 2
            },
            "DailyLoadConflict" => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = "Instructor's daily load limit would be exceeded. Move some sections to a different day.",
                SuggestionType = "TimeChange",
                Priority = 2
            },
            "CurriculumConflict" => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = "Section overlaps with another course in the same curriculum year. Change the time slot.",
                SuggestionType = "TimeChange",
                Priority = 3
            },
            "ClassroomConflict" => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = "Classroom is occupied at the requested time. Change the time slot or classroom.",
                SuggestionType = "TimeChange",
                Priority = 3
            },
            "ClassroomTypeConflict" => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = "No classroom of the required type is available. Add a classroom of the required type to the faculty.",
                SuggestionType = "ClassroomAdd",
                Priority = 5
            },
            "TheoryLabDaySplitConflict" => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = "Theory and lab components cannot be scheduled on the same day given total hours. Relax the split constraint or reduce total hours.",
                SuggestionType = "ConstraintRelax",
                Priority = 1
            },
            _ => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = $"Scheduling conflict: {conflictType}. Review constraint configuration.",
                SuggestionType = "ConstraintRelax",
                Priority = 3
            }
        };
    }
}
