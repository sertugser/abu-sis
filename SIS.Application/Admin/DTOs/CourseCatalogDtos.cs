namespace SIS.Application.Admin.DTOs;

public record CourseListItemDto(
    int Id, string Code, string Name,
    int Ects, int TheoryHours, int LabHours,
    bool IsNonGpa,
    int? DepartmentId, string? DepartmentName,
    int PrerequisiteCount, int EquivalencyCount
);

public record CourseDetailDto(
    int Id, string Code, string Name,
    int Ects, int TheoryHours, int LabHours,
    bool IsNonGpa,
    int? DepartmentId, string? DepartmentName,
    List<PrerequisiteDto> Prerequisites,
    List<EquivalencyDto> Equivalencies,
    List<CurriculumAssignmentDto> CurriculumAssignments,
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
    string Code, string Name,
    int Ects, int TheoryHours, int LabHours,
    bool IsNonGpa,
    int? DepartmentId
);

public record UpdateCourseDto(
    string Code, string Name,
    int Ects, int TheoryHours, int LabHours,
    bool IsNonGpa,
    int? DepartmentId
);

public record CourseStatsDto(
    string? LastTermName,
    int LastTermSectionCount,
    int LastTermTotalEnrolled,
    int LastTermAvgCapacity,
    int TotalTimesOpened
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
}
