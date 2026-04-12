namespace SIS.Application.Common.DTOs;

public class StudentTranscriptDto
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string FacultyName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public string StudentStatus { get; set; } = string.Empty;
    public DateTime EnrollmentDate { get; set; }
    public DateTime GeneratedAt { get; set; }
    public decimal CurrentCgpa { get; set; }
    public int TotalAttemptedCredits { get; set; }
    public int TotalAttemptedEcts { get; set; }
    public int TotalEarnedCredits { get; set; }
    public int TotalEarnedEcts { get; set; }
    public int TotalGpaEcts { get; set; }
    public decimal TotalGpaPoints { get; set; }
    public List<StudentTranscriptTermDto> Terms { get; set; } = new();
}

public class StudentTranscriptTermDto
{
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Semester { get; set; } = string.Empty;
    public int AttemptedCredits { get; set; }
    public int AttemptedEcts { get; set; }
    public int EarnedCredits { get; set; }
    public int EarnedEcts { get; set; }
    public int GpaEcts { get; set; }
    public decimal GpaPoints { get; set; }
    public int CumulativeGpaEcts { get; set; }
    public decimal CumulativeGpaPoints { get; set; }
    public decimal Sgpa { get; set; }
    public decimal Cgpa { get; set; }
    public List<StudentTranscriptCourseDto> Courses { get; set; } = new();
}

public class StudentTranscriptCourseDto
{
    public int RegistrationId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public int Ects { get; set; }
    public decimal? AverageScore { get; set; }
    public string Grade { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsRepeated { get; set; }
    public bool IsPassed { get; set; }
    public bool CountsTowardsGpa { get; set; }
    public decimal? GradePoint { get; set; }
    public decimal? WeightedEcts { get; set; }
}
