using SIS.Domain.Enums;

namespace SIS.Application.AcademicOperations.DTOs;

public class DepartmentStudentListDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int TotalStudents { get; set; }   // tüm filtreler uygulandıktan sonraki toplam (sayfalama öncesi)
    public int WithoutAdvisorCount { get; set; } // danışman filtresi hariç tüm filtrelere uyan, danışmansız öğrenci sayısı
    public int Page { get; set; } = 1;
    public int PageSize { get; set; }
    public List<DepartmentStudentListItemDto> Students { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public class DepartmentStudentListItemDto
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Cgpa { get; set; }
    public int CompletedSemesters { get; set; }
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = string.Empty;
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string ProgramCode { get; set; } = string.Empty;
    public int CurriculumId { get; set; }
    public string CurriculumVersion { get; set; } = string.Empty;
    public string CurriculumName { get; set; } = string.Empty;
    public DateTime EnrollmentDate { get; set; }
    public int? AdvisorId { get; set; }
    public string? AdvisorName { get; set; }
}

public class DepartmentCurriculumOptionDto
{
    public int CurriculumId { get; set; }
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string ProgramCode { get; set; } = string.Empty;
    public string CurriculumVersion { get; set; } = string.Empty;
    public string CurriculumName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public CurriculumStatus Status { get; set; }
    public int ActiveStudentCount { get; set; }
}

public class BulkAdvisorAssignmentRequestDto
{
    public int DepartmentId { get; set; }
    public int AdvisorId { get; set; }
    public bool ReplaceExisting { get; set; } = true;
    public List<int> StudentIds { get; set; } = new();
}

public class BulkCurriculumAssignmentRequestDto
{
    public int DepartmentId { get; set; }
    public int CurriculumId { get; set; }
    public List<int> StudentIds { get; set; } = new();
}

public class BulkAcademicOperationResultDto
{
    public int RequestedCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<BulkAcademicOperationResultItemDto> Results { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class BulkAcademicOperationResultItemDto
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
