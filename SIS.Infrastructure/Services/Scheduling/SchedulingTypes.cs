namespace SIS.Infrastructure.Services;

/// <summary>
/// SchedulingService.LoadCourseSchedulingMetadataAsync iç result tipi — kursun min suggested
/// semester'ı + per-department mapping + accessible departments.
/// </summary>
internal sealed class CourseSchedulingMetadata
{
    public Dictionary<int, int> MinSuggestedSemesters { get; init; } = new();
    public Dictionary<int, Dictionary<int, int>> SuggestedSemestersByDepartment { get; init; } = new();
    public Dictionary<int, HashSet<int>> AccessibleDepartments { get; init; } = new();
}

/// <summary>
/// S4 CoEnrollment için ortak alınan ders çiftleri (course-level). SchedulingService ve
/// SolverRequestBuilder LoadCoEnrollmentPairsAsync üretici, RuntimeSnapshot inşaası ile
/// CoEnrollmentRuntimePair'e çevirilir.
/// </summary>
public sealed class CoEnrollmentPair
{
    public int CourseId1 { get; init; }
    public int CourseId2 { get; init; }
    public int StudentCount { get; init; }
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
