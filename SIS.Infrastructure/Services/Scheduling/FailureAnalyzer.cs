using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class FailureAnalyzerResult
{
    public int SectionId { get; init; }
    public int SectionNumber { get; init; }
    public int Capacity { get; init; }
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
                : "Zamanlama kısıtlamaları nedeniyle atanamadı.";

            results.Add(new FailureAnalyzerResult
            {
                SectionId = sectionId,
                SectionNumber = snapshot.SectionNumber,
                Capacity = snapshot.Capacity,
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
            splitThreshold, facultyClassroomIds,
            splittableBlockTypes: config.SplittableBlockTypes);

        if (candidates.Count == 0)
        {
            // No candidates at all — diagnose WHY
            return DiagnoseNoCandidates(snapshot, classrooms, facultyClassroomIds);
        }

        // Candidates exist but all fail — test up to MaxCandidatesToTest
        return DiagnoseFailingCandidates(
            snapshot, candidates, state, config, constraintState, baseSnapshot, classrooms);
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
                        Message = $"Fakülteye {theoryType} tipinde derslik atanmamış. Gerekli tipte derslik ekleyin.",
                        SuggestionType = "ClassroomAdd",
                        Priority = 5
                    });
                }
                else
                {
                    suggestions.Add(new FailureSuggestionDto
                    {
                        ConstraintCode = "CapacityConflict",
                        Message = $"Fakültede {snapshot.Capacity} kişilik {theoryType} derslik yok. Derslik kapasitesini artırın veya şubeyi bölün.",
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
                        Message = $"Fakülteye {labType} tipinde derslik atanmamış. Gerekli tipte derslik ekleyin.",
                        SuggestionType = "ClassroomAdd",
                        Priority = 5
                    });
                }
                else
                {
                    suggestions.Add(new FailureSuggestionDto
                    {
                        ConstraintCode = "CapacityConflict",
                        Message = $"Fakültede {snapshot.Capacity} kişilik {labType} derslik yok. Derslik kapasitesini artırın veya şubeyi bölün.",
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
                Message = "Uygun boş zaman dilimi bulunamadı. Zaman dilimi ekleyin veya mevcut slotları boşaltın.",
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
        TermSnapshot baseSnapshot,
        List<Classroom> classrooms)
    {
        var classroomLookup = classrooms.ToDictionary(c => c.Id, c => c);
        // Collect all conflict types from testing up to MaxCandidatesToTest candidates
        var conflictTypes = new HashSet<string>();

        // Build term snapshot from current state (not empty baseSnapshot) so inter-section
        // conflicts (classroom, instructor, curriculum) are properly detected
        var termSnapshot = state.ToTermSnapshot(baseSnapshot);
        var filteredSnapshot = new TermSnapshot
        {
            TermId = termSnapshot.TermId,
            TermName = termSnapshot.TermName,
            ScopeName = termSnapshot.ScopeName,
            Sections = termSnapshot.Sections.Where(s => s.SectionId != snapshot.SectionId).ToList(),
            ClassroomAssignments = termSnapshot.ClassroomAssignments,
            DeptFacultyMap = termSnapshot.DeptFacultyMap,
            Corequisites = termSnapshot.Corequisites,
            CoEnrollmentPairs = termSnapshot.CoEnrollmentPairs,
            CohortConflictPairs = termSnapshot.CohortConflictPairs,
            AdjacentYearSoftPairs = termSnapshot.AdjacentYearSoftPairs,
            EnrollmentConflicts = termSnapshot.EnrollmentConflicts,
            DistributionConstraints = termSnapshot.DistributionConstraints,
            CourseSuggestedSemestersByDepartment = termSnapshot.CourseSuggestedSemestersByDepartment,
            CourseAccessibleDepartments = termSnapshot.CourseAccessibleDepartments,
            SlotAllowedModes = termSnapshot.SlotAllowedModes,
        };

        var toTest = candidates.Take(MaxCandidatesToTest).ToList();

        foreach (var candidate in toTest)
        {
            var testSnapshot = BuildTestSnapshot(snapshot, candidate, state, baseSnapshot, classroomLookup);

            var (conflicts, _, _) = _constraintService.EvaluateCandidate(
                testSnapshot, filteredSnapshot, config, constraintState);

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
        TermSnapshot baseSnapshot,
        Dictionary<int, Classroom> classroomLookup)
    {
        // Build TimeBlocks from the candidate's block assignments
        var timeBlocks = new List<TimeBlock>();
        int? primaryClassroomId = null;

        if (candidate.TheoryBlock != null)
        {
            timeBlocks.Add(new TimeBlock
            {
                Day = candidate.TheoryBlock.Day,
                StartTime = candidate.TheoryBlock.StartTime,
                EndTime = candidate.TheoryBlock.EndTime,
                ClassroomId = candidate.TheoryBlock.ClassroomId
            });
            primaryClassroomId = candidate.TheoryBlock.ClassroomId;
        }

        foreach (var labBlock in candidate.LabBlocks)
        {
            timeBlocks.Add(new TimeBlock
            {
                Day = labBlock.Day,
                StartTime = labBlock.StartTime,
                EndTime = labBlock.EndTime,
                ClassroomId = labBlock.ClassroomId
            });
            primaryClassroomId ??= labBlock.ClassroomId;
        }

        // Paralel split: aynı (Day, Start, End) penceresinde birden fazla classroom varsa
        // tek zaman dilimi olarak sayılır (3T+4L=7 saat → 11 değil 7). H12 CreditHourEvaluator
        // için kritik. MutableScheduleState.ToSectionSnapshot ile aynı hesap.
        var totalHours = timeBlocks
            .GroupBy(b => (b.Day, b.StartTime, b.EndTime))
            .Sum(g => (int)Math.Ceiling((g.Key.EndTime - g.Key.StartTime).TotalHours));

        // Paralel split kapasite hesabı (ConstructionSolver.MaterializeAssignedSnapshot ile aynı):
        // primary window'da birden fazla classroom varsa kapasiteleri topla.
        // Bu olmadan H3 CapacityEvaluator ClassroomCapacity=0 görüp false-positive
        // CapacityConflict suggestion üretiyordu.
        string? primaryClassroomName = snapshot.ClassroomName;
        ClassroomType primaryClassroomType = snapshot.ClassroomType;
        var effectiveCapacity = 0;
        if (timeBlocks.Count > 0)
        {
            var first = timeBlocks[0];
            var primaryWindowBlocks = timeBlocks
                .Where(b => b.Day == first.Day && b.StartTime == first.StartTime && b.EndTime == first.EndTime)
                .ToList();

            foreach (var block in primaryWindowBlocks)
            {
                if (block.ClassroomId.HasValue && classroomLookup.TryGetValue(block.ClassroomId.Value, out var cr))
                    effectiveCapacity += cr.Capacity;
            }

            if (primaryClassroomId.HasValue && classroomLookup.TryGetValue(primaryClassroomId.Value, out var primary))
            {
                primaryClassroomName = primary.Name;
                primaryClassroomType = primary.Type;
            }
        }
        if (effectiveCapacity == 0)
            effectiveCapacity = snapshot.ClassroomCapacity;

        return new SectionSnapshot
        {
            SectionId = snapshot.SectionId,
            SectionNumber = snapshot.SectionNumber,
            CourseId = snapshot.CourseId,
            CourseCode = snapshot.CourseCode,
            CourseName = snapshot.CourseName,
            TermId = snapshot.TermId,
            TheoryHours = snapshot.TheoryHours,
            LabHours = snapshot.LabHours,
            Capacity = snapshot.Capacity,
            EnrolledCount = snapshot.EnrolledCount,
            ClassroomId = primaryClassroomId,
            ClassroomName = primaryClassroomName,
            ClassroomCapacity = effectiveCapacity,
            ClassroomType = primaryClassroomType,
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
                Message = "Şube kapasitesi mevcut dersliklerin kapasitesini aşıyor. Derslik kapasitesini artırın veya şubeyi bölün.",
                SuggestionType = "CapacityIncrease",
                Priority = 1
            },
            "InstructorConflict" => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = "Öğretim üyesinin ders saati çakışması var. Başka öğretim üyesi atayın veya diğer derslerini düzenleyin.",
                SuggestionType = "InstructorReassign",
                Priority = 2
            },
            "DailyLoadConflict" => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = "Öğretim üyesinin günlük ders yükü limitini aşıyor. Dersleri farklı günlere dağıtın.",
                SuggestionType = "TimeChange",
                Priority = 2
            },
            "CurriculumConflict" => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = "Aynı müfredat yılındaki başka bir dersle çakışıyor. Zaman dilimini değiştirin.",
                SuggestionType = "TimeChange",
                Priority = 3
            },
            "ClassroomConflict" => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = "Derslik bu saatte dolu. Zaman dilimini veya dersliği değiştirin.",
                SuggestionType = "TimeChange",
                Priority = 3
            },
            "ClassroomTypeConflict" => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = "Gerekli tipte derslik mevcut değil. Fakülteye uygun tipte derslik ekleyin.",
                SuggestionType = "ClassroomAdd",
                Priority = 5
            },
            "TheoryLabDaySplitConflict" => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = "Teori ve lab bileşenleri toplam saat nedeniyle aynı güne atanamıyor. Eşik kısıtlamasını gevşetin.",
                SuggestionType = "ConstraintRelax",
                Priority = 1
            },
            _ => new FailureSuggestionDto
            {
                ConstraintCode = conflictType,
                Message = $"Zamanlama çakışması: {conflictType}. Kısıt ayarlarını gözden geçirin.",
                SuggestionType = "ConstraintRelax",
                Priority = 3
            }
        };
    }
}
