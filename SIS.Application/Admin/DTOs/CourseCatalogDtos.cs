namespace SIS.Application.Admin.DTOs;

using SIS.Domain.Enums;

public record CourseListItemDto(
    int Id, string Code, string Name, string? EnglishName,
    int Ects, int TheoryHours, int LabHours,
    bool IsNonGpa, string? ExamType,
    int? DepartmentId, string? DepartmentName,
    int PrerequisiteCount, int EquivalencyCount,
    CourseLanguage Language, TeachingMethod TeachingMethod,
    CourseStatus Status, CourseLevel Level,
    // Müfredat agregatları (Proliz tarzı tablo için)
    int NationalCredit,
    int? SemesterMin,
    int? SemesterMax,
    string RequirementType,  // "Compulsory" | "Elective" | "Mixed" | "None"
    // Aktif dönem section bilgisi (KO/DO/Kon + Öğretim Üyesi sütunları için)
    bool IsOpenInActiveTerm,
    string? ActiveTermInstructorName
);

public record CourseDetailDto(
    int Id, string Code, string Name, string? EnglishName,
    int Ects, int TheoryHours, int LabHours,
    bool IsNonGpa, string? ExamType,
    int? DepartmentId, string? DepartmentName,
    List<PrerequisiteDto> Prerequisites,
    List<EquivalencyDto> Equivalencies,
    List<CurriculumAssignmentDto> CurriculumAssignments,
    CourseLanguage Language, TeachingMethod TeachingMethod,
    CourseStatus Status, string? Description,
    CourseLevel Level, int NationalCredit,
    // Proliz "Diğer Ön Koşul Tanımları"
    int? MinCreditsRequired,
    int? MinEctsRequired,
    bool RequiresNoFailedLowerCourses,
    bool CanBeTakenStandalone,
    // Phase 2 — Proliz B "Genel Bilgiler"
    string? AlternativeName,
    bool ShowInInfoPackage,
    int? ApplicationHours,
    CourseOpenType? OpenType,
    // Plan 10 — Derslik tipi tercihleri (H4 ClassroomType encoder bunları okuyor)
    ClassroomType? TheoryClassroomType = null,
    ClassroomType? LabClassroomType = null,
    CourseStatsDto? Stats = null,
    List<PrerequisiteChainNodeDto>? PrerequisiteChain = null,
    // Bu dersi önkoşul olarak isteyen dersler (CoursePrerequisites WHERE RequiredCourseId = bu).
    // İlişkiler sekmesinde "Önkoşullar"ın simetrik karşılığı olarak gösterilir.
    List<DependentCourseDto>? DependentCourses = null
);

public record PrerequisiteDto(int CourseId, string Code, string Name);
public record EquivalencyDto(int CourseId, string Code, string Name);
public record DependentCourseDto(int CourseId, string Code, string Name);

public record CurriculumAssignmentDto(
    int CurriculumId, string ProgramName, string CurriculumVersion,
    bool IsActive, int SuggestedSemester, bool IsCompulsory
);

public record CreateCourseDto(
    string Code, string Name, string? EnglishName,
    int Ects, int TheoryHours, int LabHours,
    bool IsNonGpa, string? ExamType,
    int? DepartmentId,
    CourseLanguage Language, TeachingMethod TeachingMethod,
    CourseStatus Status, string? Description,
    CourseLevel Level, int NationalCredit,
    // Proliz "Diğer Ön Koşul Tanımları" (opsiyonel — default değerler kullanılır)
    int? MinCreditsRequired = null,
    int? MinEctsRequired = null,
    bool RequiresNoFailedLowerCourses = false,
    bool CanBeTakenStandalone = false,
    // Phase 2 — Proliz B "Genel Bilgiler"
    string? AlternativeName = null,
    bool ShowInInfoPackage = true,
    int? ApplicationHours = null,
    CourseOpenType? OpenType = null,
    // Plan 10 — Derslik tipi tercihleri (opsiyonel — null ise H4 kuralı kuralı bypass)
    ClassroomType? TheoryClassroomType = null,
    ClassroomType? LabClassroomType = null
);

public record UpdateCourseDto(
    string Code, string Name, string? EnglishName,
    int Ects, int TheoryHours, int LabHours,
    bool IsNonGpa, string? ExamType,
    int? DepartmentId,
    CourseLanguage Language, TeachingMethod TeachingMethod,
    CourseStatus Status, string? Description,
    CourseLevel Level, int NationalCredit,
    // Proliz "Diğer Ön Koşul Tanımları"
    int? MinCreditsRequired = null,
    int? MinEctsRequired = null,
    bool RequiresNoFailedLowerCourses = false,
    bool CanBeTakenStandalone = false,
    // Phase 2 — Proliz B "Genel Bilgiler"
    string? AlternativeName = null,
    bool ShowInInfoPackage = true,
    int? ApplicationHours = null,
    CourseOpenType? OpenType = null,
    // Plan 10 — Derslik tipi tercihleri (opsiyonel — null ise H4 kuralı bypass)
    ClassroomType? TheoryClassroomType = null,
    ClassroomType? LabClassroomType = null
);

public record CourseStatsDto(
    // Genel
    int TotalTimesOpened,
    int TotalStudentsAllTime,
    int PrerequisiteForCount,
    // Son dönem
    string? LastTermName,
    int LastTermSectionCount,
    int LastTermTotalEnrolled,
    int LastTermTotalCapacity,
    // Doluluk
    double LastTermFillRate,
    // Not dağılımı (tüm dönemler)
    Dictionary<string, int> GradeDistribution,
    double PassRate,
    // Bırakma/Çekilme
    int TotalWithdrawn,
    int TotalDropped,
    // Dönem bazlı trend (son 5 dönem)
    List<TermTrendDto> TermTrends
);

public record TermTrendDto(
    string TermName,
    int SectionCount,
    int Enrolled,
    int Capacity
);

public record PrerequisiteChainNodeDto(
    int CourseId, string Code, string Name,
    List<PrerequisiteChainNodeDto> Prerequisites
);

public record AddPrerequisiteDto(int RequiredCourseId);
public record AddEquivalencyDto(int EquivalentCourseId);

// Proliz "Diğer Ön Koşul Tanımları" — sadece 4 kuralı güncellemek için ayrı endpoint
public record UpdateCoursePrereqRulesDto(
    int? MinCreditsRequired,
    int? MinEctsRequired,
    bool RequiresNoFailedLowerCourses,
    bool CanBeTakenStandalone
);

public record CourseFilterDto
{
    public string? Search { get; set; }
    public int? DepartmentId { get; set; }
    public int? FacultyId { get; set; }
    public CourseLanguage? Language { get; set; }
    public CourseLevel? Level { get; set; }
    public CourseStatus? Status { get; set; }
}
