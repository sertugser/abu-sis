namespace SIS.Application.AcademicProgression.DTOs;

/// <summary>Toplu sınıf atlatma sonuç DTO'su</summary>
public class BatchProgressionResultDto
{
    public int TotalStudentsAnalyzed { get; set; }
    public int PromotedCount { get; set; }
    public int StayedCount { get; set; }
    public int WarningCount { get; set; }
    public List<StudentProgressionDto> Results { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public class StudentProgressionDto
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int OldLevel { get; set; }
    public string OldLevelLabel { get; set; } = string.Empty;
    public int NewLevel { get; set; }
    public string NewLevelLabel { get; set; } = string.Empty;
    public int CompletedEcts { get; set; }
    public decimal Cgpa { get; set; }
    public decimal Sgpa { get; set; }
    public bool WasPromoted { get; set; }
    public string Status { get; set; } = string.Empty; // "Normal", "Warning-LowCGPA", "Warning-BehindSchedule"
    public List<string> Warnings { get; set; } = new();
    public List<string> PromotionRuleDetails { get; set; } = new();
}

/// <summary>Tek öğrenci dönem sonu GPA yeniden hesaplama sonucu</summary>
public class GpaRecalculationResultDto
{
    public int TotalStudents { get; set; }
    public int RecalculatedCount { get; set; }
    public int ChangedCount { get; set; }
    public List<string> Notes { get; set; } = new();
}

/// <summary>Dönem kapatma -- not girişi kilitleme sonucu (placeholder)</summary>
public class GradeLockResultDto
{
    public int TotalSections { get; set; }
    public int TotalActiveRegistrations { get; set; }
    public int MissingGradeCount { get; set; }
    public bool AllGradesEntered { get; set; }
    public bool HasActiveRegistrations { get; set; }
    public List<string> Notes { get; set; } = new();
}
