namespace SIS.Application.CourseRegistration.DTOs;

public class ValidationResultDto
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
}

public class ValidationError
{
    public string Rule { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? CourseSectionId { get; set; }
}
