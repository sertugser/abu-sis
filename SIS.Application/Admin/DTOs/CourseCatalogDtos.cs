namespace SIS.Application.Admin.DTOs;

using SIS.Domain.Enums;

public record CourseListItemDto(
    int Id, string Code, string Name, string? EnglishName,
    int Ects, int TheoryHours, int LabHours,
    bool IsNonGpa, string? ExamType,
    int? DepartmentId, string? DepartmentName,
    int PrerequisiteCount, int EquivalencyCount,
    CourseLanguage Language, TeachingMethod TeachingMethod,
    CourseStatus Status, CourseLevel Level
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
    CourseLevel Level, int? NationalCredit,
    CourseStatsDto? Stats = null,
    List<PrerequisiteChainNodeDto>? PrerequisiteChain = null
);

public record PrerequisiteDto(int CourseId, string Code, string Name);
public record EquivalencyDto(int CourseId, string Code, string Name);

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
    CourseLevel Level, int? NationalCredit
);

public record UpdateCourseDto(
    string Code, string Name, string? EnglishName,
    int Ects, int TheoryHours, int LabHours,
    bool IsNonGpa, string? ExamType,
    int? DepartmentId,
    CourseLanguage Language, TeachingMethod TeachingMethod,
    CourseStatus Status, string? Description,
    CourseLevel Level, int? NationalCredit
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

public record AssignToCurriculumDto(
    int CurriculumId,
    int SuggestedSemester,
    bool IsCompulsory
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
