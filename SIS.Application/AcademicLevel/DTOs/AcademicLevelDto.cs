namespace SIS.Application.AcademicLevel.DTOs;

public class AcademicLevelDto
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    // Resmi sinif etiketi (kayit yili bazli)
    public int OfficialClassLevel { get; set; }
    public string OfficialClassLabel { get; set; } = string.Empty;   // "1. Sinif", "2. Sinif", ...

    // Akademik seviye (ilerleme bazli)
    public int AcademicLevel { get; set; }
    public string AcademicLevelLabel { get; set; } = string.Empty;

    // ECTS ilerleme
    public int CompletedEcts { get; set; }
    public int TotalRequiredEcts { get; set; }
    public decimal EctsCompletionRate { get; set; }

    // Donem bilgisi
    public int CompletedSemesters { get; set; }
    public int FrozenSemesters { get; set; }

    // Yil bazli mufredat tamamlanma durumu
    public List<YearProgressDto> YearProgress { get; set; } = new();

    // Uyumsuzluk varsa
    public bool HasDiscrepancy { get; set; }
    public string? DiscrepancyNote { get; set; }
}

public class YearProgressDto
{
    public int Year { get; set; }
    public string YearLabel { get; set; } = string.Empty;           // "1. Sinif", "2. Sinif", ...
    public int TotalMandatoryCourses { get; set; }
    public int CompletedMandatoryCourses { get; set; }
    public decimal CompletionRate { get; set; }
    public int TotalEcts { get; set; }
    public int CompletedEcts { get; set; }
}

public class BulkAcademicLevelDto
{
    public int TotalStudents { get; set; }
    public int DiscrepancyCount { get; set; }
    public List<LevelDistributionDto> OfficialDistribution { get; set; } = new();
    public List<LevelDistributionDto> AcademicDistribution { get; set; } = new();
    public List<AcademicLevelDto> Students { get; set; } = new();
}

public class LevelDistributionDto
{
    public int Level { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}
