using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.FacultyPlanning.DTOs;

public class AutoScheduleRequest
{
    public int FacultyId { get; set; }
    public int TermId { get; set; }
    public int? DepartmentId { get; set; }
    public List<int>? SectionIds { get; set; }
    public int? Seed { get; set; }
}

public class AutoSchedulePreviewDto
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<AutoSchedulePreviewItemDto> Assignments { get; set; } = new();
    public List<AutoScheduleFailureDto> Failures { get; set; } = new();
    /// <summary>
    /// Engine çalışırken kural ihlali bulunan mevcut atamalar temizlenip yeniden atandıysa
    /// bu listede rapor edilir. UI admin'e "şu dersler X sebebiyle sıfırlandı ve yeniden yerleştirildi" gösterir.
    /// </summary>
    public List<InvalidatedAssignmentDto> InvalidatedAssignments { get; set; } = new();
    public int Seed { get; set; }
    public double TotalPenalty { get; set; }
    public AutoScheduleStats Stats { get; set; } = new();
}

public class InvalidatedAssignmentDto
{
    public int SectionId { get; set; }
    public int SectionNumber { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class AutoSchedulePreviewItemDto
{
    public int SectionId { get; set; }
    public int SectionNumber { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public int? ClassroomId { get; set; }
    public string ClassroomName { get; set; } = string.Empty;
    public List<ScheduleSlotDto> ScheduleSlots { get; set; } = new();
    public string ScheduleSummary { get; set; } = string.Empty;
    public int? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public decimal FitScore { get; set; }
}

public class AutoScheduleFailureDto
{
    public int SectionId { get; set; }
    public int SectionNumber { get; set; }
    public int Capacity { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<FailureSuggestionDto> Suggestions { get; set; } = new();
}

public class FailureSuggestionDto
{
    public string ConstraintCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SuggestionType { get; set; } = string.Empty;
    public int Priority { get; set; }
}

/// <summary>
/// Auto-schedule sonuç istatistikleri. v2-ortools tek-geçişli CP-SAT — v1'in (Greedy + SA + LNS)
/// aşama bazlı alanları (ConstructionPlaced/Failed, SA*, vb.) Plan 9 sonrası kaldırıldı.
/// Frontend bu alanları sonuç dialog'unda kullanır; tüm sayılar backend kanonik kaynaklıdır.
/// </summary>
public class AutoScheduleStats
{
    /// <summary>Solver toplam çalışma süresi (saniye).</summary>
    public double ElapsedSeconds { get; set; }

    /// <summary>İşlenen şubelerin kapsadığı farklı <c>Course</c> sayısı (success + failure birleşik).</summary>
    public int DistinctCourseCount { get; set; }

    /// <summary>Başarılı atamaların kullandığı farklı <c>Classroom</c> sayısı.</summary>
    public int UsedClassroomCount { get; set; }
}

public class ApplyScheduleRequest
{
    public List<ApplyScheduleItemRequest> Assignments { get; set; } = new();
}

public class ApplyScheduleItemRequest
{
    public int SectionId { get; set; }
    public int? ClassroomId { get; set; }
    public List<ScheduleSlotDto> ScheduleSlots { get; set; } = new();
    public int? InstructorId { get; set; }
}

public class ScheduleRunSummaryDto
{
    public int Id { get; set; }
    public int Seed { get; set; }
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public double TotalPenalty { get; set; }
    public double ElapsedSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ScheduleRunDetailDto
{
    public int Id { get; set; }
    public int Seed { get; set; }
    public DateTime CreatedAt { get; set; }
    public AutoSchedulePreviewDto Result { get; set; } = null!;
}

public class ApplyScheduleResultDto
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public bool RolledBack { get; set; }
    public List<string> Errors { get; set; } = new();
}
