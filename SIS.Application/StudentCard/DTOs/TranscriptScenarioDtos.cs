namespace SIS.Application.StudentCard.DTOs;

/// <summary>
/// Transkript Senaryosu (what-if) hesabı için İSTEK. Tüm ders verisi öğrencinin ekranındaki
/// simülasyondan gelir — sistemde kalıcı değişiklik yapılmaz, salt bilgilendirme. Hesap
/// backend'de TEK kaynaktan (GradePointMap + GpaMath kuralları) yapılır; frontend yalnızca
/// girdiyi toplar ve sonucu gösterir (ikibaşlılık yok).
/// </summary>
public sealed class TranscriptScenarioRequestDto
{
    public List<ScenarioTermInputDto> Terms { get; set; } = new();
    public List<ScenarioCourseInputDto> Courses { get; set; } = new();
}

public sealed class ScenarioTermInputDto
{
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Semester { get; set; } = string.Empty;
    public int Order { get; set; }
}

public sealed class ScenarioCourseInputDto
{
    public string ScenarioId { get; set; } = string.Empty;
    public int? SourceRegistrationId { get; set; }
    public int? CourseId { get; set; }
    public int TermId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public int Ects { get; set; }
    public decimal? AverageScore { get; set; }
    public string Grade { get; set; } = string.Empty;
    public string? OriginalGrade { get; set; }
    public bool ExcludeFromAverage { get; set; }
    public string ContinuationCourseId { get; set; } = string.Empty;
    public bool IsAdded { get; set; }
    public bool IsPlannedFromCurriculum { get; set; }
    public bool WasRepeated { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// Transkript Senaryosu hesabının SONUCU. Frontend bu sonucu doğrudan render eder; kendi
/// GPA/AKTS/geçti hesabını yapmaz.
/// </summary>
public sealed class ScenarioReportDto
{
    /// <summary>GPA'ya giren ders yoksa null ("veri yok" → UI "—"); sahte 0,00 değil.</summary>
    public decimal? CurrentCgpa { get; set; }
    public int TotalAttemptedCredits { get; set; }
    public int TotalAttemptedEcts { get; set; }
    public int TotalEarnedCredits { get; set; }
    public int TotalEarnedEcts { get; set; }
    public int TotalGpaEcts { get; set; }
    public decimal TotalGpaPoints { get; set; }
    public List<ScenarioTermReportDto> Terms { get; set; } = new();
}

public sealed class ScenarioTermReportDto
{
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Semester { get; set; } = string.Empty;
    public int AttemptedCredits { get; set; }
    public int AttemptedEcts { get; set; }
    public int EarnedCredits { get; set; }
    public int EarnedEcts { get; set; }
    public int GpaEcts { get; set; }
    public decimal GpaPoints { get; set; }
    public int CumulativeGpaEcts { get; set; }
    public decimal CumulativeGpaPoints { get; set; }
    /// <summary>Dönem ortalaması; GPA'ya giren ders yoksa null ("—").</summary>
    public decimal? Sgpa { get; set; }
    /// <summary>Genel ortalama (o döneme kadar); GPA'ya giren ders yoksa null ("—").</summary>
    public decimal? Cgpa { get; set; }
    public List<ScenarioComputedCourseDto> Courses { get; set; } = new();
}

public sealed class ScenarioComputedCourseDto
{
    public string ScenarioId { get; set; } = string.Empty;
    public int? SourceRegistrationId { get; set; }
    public int? CourseId { get; set; }
    public int TermId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public int Ects { get; set; }
    public decimal? AverageScore { get; set; }
    public string Grade { get; set; } = string.Empty;
    public string? OriginalGrade { get; set; }
    public bool ExcludeFromAverage { get; set; }
    public string ContinuationCourseId { get; set; } = string.Empty;
    public bool IsAdded { get; set; }
    public bool IsPlannedFromCurriculum { get; set; }
    public int SortOrder { get; set; }

    // Computed (tek kaynak: GradePointMap + GpaMath)
    public decimal? GradePoint { get; set; }
    public bool CountsTowardsGpa { get; set; }
    public bool IsPassed { get; set; }
    public decimal? WeightedEcts { get; set; }
    public string ChainId { get; set; } = string.Empty;
    public bool IsRepeated { get; set; }
}
