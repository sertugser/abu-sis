namespace SIS.Application.Admin.DTOs;

public record FacultyDto(int Id, string Name, string Code, int DepartmentCount);

public record DepartmentDto(int Id, string Name, string Code, int FacultyId, string FacultyName, int ProgramCount);

public record ProgramDto(
    int Id, string Name, string Code,
    int DepartmentId, string DepartmentName,
    string DegreeLevel, string EducationType,
    int MaxStudyDurationSemesters, int TotalRequiredEcts,
    int CurriculumCount
);

public record CreateFacultyDto(string Name, string Code);
public record UpdateFacultyDto(string Name, string Code);

public record CreateDepartmentDto(string Name, string Code, int FacultyId);
public record UpdateDepartmentDto(string Name, string Code, int FacultyId);

public record CreateProgramDto(
    string Name, string Code,
    int DepartmentId,
    string DegreeLevel,
    string EducationType,
    int MaxStudyDurationSemesters,
    int TotalRequiredEcts
);
public record UpdateProgramDto(
    string Name, string Code,
    int DepartmentId,
    string DegreeLevel,
    string EducationType,
    int MaxStudyDurationSemesters,
    int TotalRequiredEcts
);
