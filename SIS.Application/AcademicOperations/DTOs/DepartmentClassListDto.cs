namespace SIS.Application.AcademicOperations.DTOs;

public class DepartmentClassListDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int TotalSections { get; set; }
    public int TotalStudents { get; set; }
    public List<DepartmentSectionClassListDto> Sections { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public class DepartmentSectionClassListDto
{
    public int SectionId { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public string ScheduleSummary { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public int ActiveStudentCount { get; set; }
    public bool IsActive { get; set; }
    public List<DepartmentSectionStudentDto> Students { get; set; } = new();
}

public class DepartmentSectionStudentDto
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Grade { get; set; }
}
