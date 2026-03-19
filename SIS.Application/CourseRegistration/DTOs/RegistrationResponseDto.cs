namespace SIS.Application.CourseRegistration.DTOs;

public class RegistrationResponseDto
{
    public bool Success { get; set; }
    public List<RegistrationItemDto> Registrations { get; set; } = new();
    public ValidationResultDto? ValidationResult { get; set; }
}

public class RegistrationItemDto
{
    public int Id { get; set; }
    public int CourseSectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Ects { get; set; }
    public string Status { get; set; } = string.Empty;
}
