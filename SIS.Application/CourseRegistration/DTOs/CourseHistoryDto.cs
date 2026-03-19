namespace SIS.Application.CourseRegistration.DTOs;

public class CourseHistoryTermDto
{
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Semester { get; set; } = string.Empty;
    public List<CourseHistoryItemDto> Courses { get; set; } = new();
}

public class CourseHistoryItemDto
{
    public int RegistrationId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Ects { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsRepeated { get; set; }
}
