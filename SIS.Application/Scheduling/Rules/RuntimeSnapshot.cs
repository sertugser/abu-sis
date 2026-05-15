using SIS.Domain.Enums;

namespace SIS.Application.Scheduling.Rules;

/// <summary>
/// Plan 11 — Term-scope read-only snapshot. RuleEngine bu yapıyı predicate'lara aktarır.
/// V1 <c>TermSnapshot</c>'ın yeni adı; CP-SAT'a özgü alanlar (SectionInput) yok, sadece
/// runtime check için gerekli veri.
/// </summary>
public sealed class RuntimeSnapshot
{
    public required int TermId { get; init; }
    public string TermName { get; init; } = string.Empty;
    public string ScopeName { get; init; } = string.Empty;

    public required IReadOnlyList<RuntimeCandidate> Sections { get; init; }

    /// <summary>H10 FacultyClassroom için: ClassroomId ↔ FacultyId atamaları.</summary>
    public IReadOnlyList<ClassroomTermAssignmentInfo> ClassroomAssignments { get; init; }
        = Array.Empty<ClassroomTermAssignmentInfo>();

    /// <summary>H10: DepartmentId ↔ FacultyId hızlı lookup.</summary>
    public IReadOnlyDictionary<int, int> DeptFacultyMap { get; init; }
        = new Dictionary<int, int>();

    /// <summary>H11: Corequisite course çiftleri.</summary>
    public IReadOnlyList<CorequisitePair> Corequisites { get; init; }
        = Array.Empty<CorequisitePair>();

    /// <summary>H17: (Day, StartTime) → AllowedMode lookup.</summary>
    public IReadOnlyDictionary<(int Day, TimeOnly StartTime), TimeSlotAllowedMode> SlotAllowedModes { get; init; }
        = new Dictionary<(int, TimeOnly), TimeSlotAllowedMode>();

    /// <summary>H5 cohort mode: pre-computed conflict çiftleri.</summary>
    public IReadOnlySet<(int, int)> CohortConflictPairs { get; init; }
        = new HashSet<(int, int)>();

    /// <summary>H5 mode: "curriculum" (default) | "cohort" | "enrollment".</summary>
    public string CurriculumConflictMode { get; init; } = "curriculum";

    /// <summary>H5 enrollment mode: gerçek kayıt verisinden hard çakışma çiftleri.</summary>
    public EnrollmentConflictMatrix? EnrollmentConflicts { get; init; }

    /// <summary>
    /// Plan 12 — S4 CoEnrollment için: ortak alınan course çiftleri ve öğrenci sayıları.
    /// <c>SchedulingService.LoadCoEnrollmentPairsAsync</c> tarafından (S4 enabled ise) doldurulur.
    /// </summary>
    public IReadOnlyList<CoEnrollmentRuntimePair> CoEnrollmentPairs { get; init; }
        = Array.Empty<CoEnrollmentRuntimePair>();

    /// <summary>
    /// Plan 12 — S20 AdjacentYear için: komşu yıl ders çiftleri (sec1, sec2).
    /// <c>TermSnapshot.AdjacentYearSoftPairs</c> portu — RuntimeSnapshotAdapter doldurur.
    /// </summary>
    public IReadOnlySet<(int FirstSectionId, int SecondSectionId)> AdjacentYearSoftPairs { get; init; }
        = new HashSet<(int, int)>();

    /// <summary>
    /// Plan 12 — DIST DistributionConstraint preload set'i. Per-instructor slot tercihleri.
    /// </summary>
    public IReadOnlyList<DistributionConstraintInfo> DistributionConstraints { get; init; }
        = Array.Empty<DistributionConstraintInfo>();
}

public sealed record ClassroomTermAssignmentInfo(int ClassroomId, int FacultyId);
public sealed record CorequisitePair(int CourseId, int RequiredCourseId);

/// <summary>
/// Plan 12 — S4 CoEnrollment pair (Course ↔ Course, common student count).
/// V1 <see cref="SIS.Infrastructure.Services.Scheduling.CoEnrollmentPair"/> portu.
/// </summary>
public sealed record CoEnrollmentRuntimePair(int FirstCourseId, int SecondCourseId, int StudentCount);

/// <summary>
/// Plan 12 — DIST DistributionConstraint per-section slot tercih bilgisi.
/// Level değerleri: Unavailable (hard ban) → Evaluator hard ihlal; Discouraged/Preferred → soft penalty.
/// </summary>
public sealed record DistributionConstraintInfo(
    int InstructorId,
    int Day,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DistributionPreferenceLevel Level);

public enum DistributionPreferenceLevel
{
    Unavailable = 0,
    StronglyDiscouraged = 1,
    Discouraged = 2,
    Neutral = 3,
    Preferred = 4,
}

/// <summary>
/// Plan 11 — H5 enrollment mode (`Schedule.CurriculumConflictMode = "enrollment"`) için
/// gerçek öğrenci kayıt verisinden çıkarılan hard çakışma çiftleri.
/// V1 <c>EnrollmentConflictsMatrix</c> portu (yer kontrolü Faz 3.5'te).
/// </summary>
public sealed class EnrollmentConflictMatrix
{
    public IReadOnlySet<(int, int)> HardConflictPairs { get; init; } = new HashSet<(int, int)>();
}
