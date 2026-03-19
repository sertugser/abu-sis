namespace SIS.Application.CourseRegistration.DTOs;

public class StudentRegistrationDto
{
    public int RegistrationId { get; set; }
    public int CourseSectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Ects { get; set; }
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsCompulsory { get; set; }
    public int SuggestedSemester { get; set; }
    public bool IsRepeated { get; set; }
    public string TakeType { get; set; } = string.Empty;
}
