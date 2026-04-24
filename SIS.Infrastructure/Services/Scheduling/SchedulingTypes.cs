using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Infrastructure.Services;

public sealed class TermSnapshot
{
    public int TermId { get; init; }
    public string TermName { get; init; } = string.Empty;
    public string ScopeName { get; init; } = string.Empty;
    public List<SectionSnapshot> Sections { get; init; } = new();
    public List<ClassroomTermAssignment> ClassroomAssignments { get; init; } = new(); // for H10
    public Dictionary<int, int> DeptFacultyMap { get; init; } = new();                // for H10
    public List<CoursePrerequisite> Corequisites { get; init; } = new();              // for H11
    public List<CoEnrollmentPair> CoEnrollmentPairs { get; set; } = new();            // for S4
    public HashSet<(int SectionId1, int SectionId2)>? CohortConflictPairs { get; set; }
    public EnrollmentConflictMatrix? EnrollmentConflicts { get; set; }
    public List<DistributionConstraint>? DistributionConstraints { get; set; }
    public Dictionary<int, Dictionary<int, int>> CourseSuggestedSemestersByDepartment { get; init; } = new();
    public Dictionary<int, HashSet<int>> CourseAccessibleDepartments { get; init; } = new();
    /// <summary>
    /// (DayOfWeek, StartTime) -> TimeSlot.AllowedMode. FaceToFaceSlotRestriction evaluator'ı için.
    /// </summary>
    public Dictionary<(DayOfWeek Day, TimeOnly StartTime), TimeSlotAllowedMode> SlotAllowedModes { get; init; } = new();
}

internal sealed class CourseSchedulingMetadata
{
    public Dictionary<int, int> MinSuggestedSemesters { get; init; } = new();
    public Dictionary<int, Dictionary<int, int>> SuggestedSemestersByDepartment { get; init; } = new();
    public Dictionary<int, HashSet<int>> AccessibleDepartments { get; init; } = new();
}

public sealed class CoEnrollmentPair
{
    public int CourseId1 { get; init; }
    public int CourseId2 { get; init; }
    public int StudentCount { get; init; }
}

public sealed class SectionSnapshot
{
    public int SectionId { get; init; }
    public int SectionNumber { get; init; }
    public int CourseId { get; init; }
    public string CourseCode { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public int TermId { get; init; }
    public TeachingMethod TeachingMethod { get; init; } = TeachingMethod.FaceToFace;
    public int TheoryHours { get; init; }
    public int LabHours { get; init; }
    public int Capacity { get; init; }
    public int EnrolledCount { get; init; }
    public int? ClassroomId { get; init; }
    public string ClassroomName { get; init; } = string.Empty;
    public int ClassroomCapacity { get; init; }
    public ClassroomType ClassroomType { get; init; }         // for H4
    public ClassroomType? TheoryClassroomType { get; init; }
    public ClassroomType? LabClassroomType { get; init; }
    public int? InstructorId { get; init; }
    public string InstructorName { get; init; } = string.Empty;
    public int InstructorMaxWeeklyHours { get; init; }
    public int? DepartmentId { get; init; }                    // for H5, H8
    public int? SuggestedSemester { get; init; }               // for H5 (curriculum year)
    public Dictionary<int, int> SuggestedSemestersByDepartment { get; init; } = new();
    public HashSet<int> AccessibleDepartmentIds { get; init; } = new();
    public int AssignedTimeSlotCount { get; init; }
    public List<TimeBlock> TimeBlocks { get; init; } = new();
    public List<string> Notes { get; init; } = new();
}

internal sealed class ScheduleParseResult
{
    public List<TimeBlock> Slots { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

internal sealed class RankedSlot
{
    public TimeBlock Slot { get; init; } = null!;
    public int PreferencePenalty { get; init; }
}

internal sealed class RankedSlotCombination
{
    public List<TimeBlock> Slots { get; init; } = new();
    public int PreferenceScore { get; init; }
}
