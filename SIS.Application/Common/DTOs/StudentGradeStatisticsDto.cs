namespace SIS.Application.Common.DTOs;

public class StudentGradeStatisticsDto
{
    public int RegistrationId { get; set; }
    public int CourseSectionId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public string FacultyProgram { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int SectionNumber { get; set; }
    public string? StudentLetterGrade { get; set; }
    public decimal? StudentWeightedAverage { get; set; }
    public bool HasGradingCriteria { get; set; }
    public bool HasAnyComponentScore { get; set; }
    public bool HasAllComponentScores { get; set; }
    public bool IsResultFinalized { get; set; }
    public bool IsCommonEvaluation { get; set; }
    public StudentResultInfoDto Result { get; set; } = new();
    public List<StudentGradeRangeDto> GradeRanges { get; set; } = new();
    /// <summary>
    /// Bütünleme harf dağılımı. Karar (2026-05-30): aynı <see cref="GradeRanges"/> aralık tablosu
    /// kullanılır (üniversite tek puan→harf kuralında); sadece <c>StudentCount</c> büt notu üzerinden
    /// yeniden sayılır. Bütünlemeye giren yoksa boş liste döner.
    /// </summary>
    public List<StudentGradeRangeDto> MakeupGradeRanges { get; set; } = new();
    public List<StudentComponentStatisticsDto> Components { get; set; } = new();
    public StudentSectionStatisticsSummaryDto Summary { get; set; } = new();
}

public class StudentResultInfoDto
{
    public string Status { get; set; } = string.Empty;
    public DateTime? StatusDate { get; set; }
    public string EvaluationMethod { get; set; } = string.Empty;
}

public class StudentGradeRangeDto
{
    public string Grade { get; set; } = string.Empty;
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
    public int StudentCount { get; set; }
}

public class StudentComponentStatisticsDto
{
    public int ComponentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public int TotalStudentCount { get; set; }
    public int EnteredStudentCount { get; set; }
    public int MissingStudentCount { get; set; }
    /// <summary>
    /// Sınava "Devamsızlık nedeniyle giremedi" işaretli öğrenci sayısı
    /// (<see cref="SIS.Domain.Enums.ExamAttendance.Absent"/>).
    /// MissingStudentCount'un alt kümesi değil; UI bunu ayrı satırda gösterir.
    /// </summary>
    public int AbsentByAttendanceCount { get; set; }
    public decimal? AverageScore { get; set; }
    public decimal? StudentScore { get; set; }
    /// <summary>
    /// Hoca bu komponenti "ilan et" dediği an (UTC). null = ilan edilmedi.
    /// Tek-kaynak: <see cref="SIS.Domain.Entities.GradeComponent.PublishedAt"/>.
    /// </summary>
    public DateTime? PublishedAt { get; set; }
    /// <summary>
    /// Öğrencinin bu komponente girip girmediği. "G" (Girmedi/Absent) ve "M" (Mazeret/Excused)
    /// kısaltmaları UI'da puan yerine gösterilir.
    /// </summary>
    public string StudentExamAttendance { get; set; } = "NotMarked";
}

public class StudentSectionStatisticsSummaryDto
{
    public int TotalStudentCount { get; set; }
    public int GradedStudentCount { get; set; }
    public decimal? ClassAverage { get; set; }
}
