namespace SIS.Application.AcademicOperations.DTOs;

public class DepartmentCurriculumOverviewDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int ProgramCount { get; set; }
    public int ActiveCurriculumCount { get; set; }
    public int TotalCourses { get; set; }
    public int TotalEcts { get; set; }
    public List<ProgramCurriculumOverviewDto> Programs { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public class ProgramCurriculumOverviewDto
{
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string ProgramCode { get; set; } = string.Empty;
    public int CurriculumId { get; set; }
    public string CurriculumVersion { get; set; } = string.Empty;
    public bool IsActiveCurriculum { get; set; }
    public int TotalCourses { get; set; }
    public int TotalCredits { get; set; }
    public int TotalEcts { get; set; }
    public int CompulsoryCourseCount { get; set; }
    public int ElectiveCourseCount { get; set; }
    public List<CurriculumSemesterOverviewDto> SemesterSummaries { get; set; } = new();
}

public class CurriculumSemesterOverviewDto
{
    public int SemesterNumber { get; set; }
    public string Label { get; set; } = string.Empty;
    public int CourseCount { get; set; }
    public int Credits { get; set; }
    public int Ects { get; set; }
    public int CompulsoryCourseCount { get; set; }
    public int ElectiveCourseCount { get; set; }
}
