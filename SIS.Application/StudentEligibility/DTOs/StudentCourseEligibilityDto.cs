using SIS.Domain.Enums;

namespace SIS.Application.StudentEligibility.DTOs;

public class StudentCourseEligibilityDto
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Ects { get; set; }
    public bool IsCompulsory { get; set; }
    public int SuggestedSemester { get; set; }
    public bool IsEligible { get; set; }
    public string TakeType { get; set; } = string.Empty;      // MustRetake, Regular, BehindSchedule, UpperCourse
    public GradeCode? PreviousGrade { get; set; }
    public int CurrentSemester { get; set; }
    public decimal Cgpa { get; set; }
    public int CurrentTermEcts { get; set; }
    public int EctsLimit { get; set; }
    public List<IneligibilityReasonDto> Reasons { get; set; } = new();
}

public class IneligibilityReasonDto
{
    public IneligibilityReason Reason { get; set; }
    public string Message { get; set; } = string.Empty;
}
