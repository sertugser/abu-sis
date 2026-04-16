using SIS.Domain.Enums;

namespace SIS.Application.TermClosing.DTOs;

public class TermClosingStatusDto
{
    public int DepartmentId { get; set; }
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public List<TermClosingStepStatus> Steps { get; set; } = new();
}

public class TermClosingStepStatus
{
    public TermClosingStep Step { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByName { get; set; }
    public string? Notes { get; set; }
}

public class TermClosingStepResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class NewTermRequestDto
{
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public SemesterType Semester { get; set; }
    public DateTime RegistrationStart { get; set; }
    public DateTime RegistrationEnd { get; set; }
    public DateTime TermStart { get; set; }
    public DateTime TermEnd { get; set; }
}

public class NewTermResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? NewTermId { get; set; }
}

/// <summary>Şube bazlı not giriş detayı</summary>
public class SectionGradeDetailDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string SectionCode { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public int TotalRegistrations { get; set; }
    public int EnteredGradeCount { get; set; }
    public int MissingGradeCount { get; set; }
    public bool IsFinalized { get; set; }
    public decimal EntryPercentage { get; set; }
}

/// <summary>Not kilitleme hazırlık detayı — özet + şube kırılımı</summary>
public class GradeLockDetailResultDto
{
    public int TotalSections { get; set; }
    public int TotalActiveRegistrations { get; set; }
    public int MissingGradeCount { get; set; }
    public bool AllGradesEntered { get; set; }
    public bool HasActiveRegistrations { get; set; }
    public int FinalizedSectionCount { get; set; }
    public decimal OverallEntryPercentage { get; set; }
    public List<SectionGradeDetailDto> Sections { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

/// <summary>Harf notu dağılımı satırı</summary>
public class GradeDistributionItemDto
{
    public string GradeCode { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>Not dağılım özeti</summary>
public class GradeDistributionResultDto
{
    public int TotalGraded { get; set; }
    public decimal AverageScore { get; set; }
    public decimal PassRate { get; set; }
    public List<GradeDistributionItemDto> Grades { get; set; } = new();
}

/// <summary>Dekan görünümü — bölüm bazlı dönem kapama özeti</summary>
public class DepartmentClosingOverviewDto
{
    public int DepartmentId { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public int CompletedStepCount { get; set; }
    public string CurrentStepLabel { get; set; } = string.Empty;
    public int TotalSections { get; set; }
    public int MissingGradeCount { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public string? LastActivityByName { get; set; }
}
