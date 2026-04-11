namespace SIS.Application.CourseRegistration.DTOs;

public class PendingApprovalDto
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public decimal Cgpa { get; set; }
    public decimal Sgpa { get; set; }
    public int CompletedSemesters { get; set; }
    public bool IsScholarshipStudent { get; set; }
    public List<PendingRegistrationItemDto> Registrations { get; set; } = new();
    public int TotalEcts { get; set; }
    public int EctsLimit { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string PlanStatus { get; set; } = string.Empty;

    // Alttan dersler (FF/NA/U)
    public List<FailedCourseDto> FailedCourses { get; set; } = new();

    // Müfredat özeti
    public int CurriculumTotalCourses { get; set; }
    public int CurriculumCompletedCourses { get; set; }
    public int CurriculumTotalEcts { get; set; }
    public int CurriculumCompletedEcts { get; set; }
}

public class FailedCourseDto
{
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public bool IsSelectedInPlan { get; set; }
}

public class PendingRegistrationItemDto
{
    public int RegistrationId { get; set; }
    public int CourseSectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Ects { get; set; }
    public string Schedule { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public bool IsRepeated { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasScheduleConflict { get; set; }
}

public class ApprovalActionDto
{
    public int StudentId { get; set; }
    public string? Note { get; set; }
}

public class ApprovalResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int AffectedRegistrations { get; set; }
}
