namespace SIS.Application.StudentEligibility.DTOs;

public class CourseEligibilityReportDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Ects { get; set; }
    public int TotalEligible { get; set; }
    public int TotalIneligible { get; set; }

    public EligibilityBreakdownDto Breakdown { get; set; } = new();
    public List<StudentCourseEligibilityDto> EligibleStudents { get; set; } = new();
    public List<StudentCourseEligibilityDto> IneligibleStudents { get; set; } = new();
}

public class EligibilityBreakdownDto
{
    public int MustRetakeCount { get; set; }
    public int RegularCount { get; set; }
    public int BehindScheduleCount { get; set; }
    public int UpperCourseCount { get; set; }

    public int PrerequisiteNotMetCount { get; set; }
    public int EctsLimitExceededCount { get; set; }
    public int AlreadyPassedCount { get; set; }
    public int LowCgpaRestrictionCount { get; set; }
    public int FinancialBlockCount { get; set; }
    public int OtherIneligibleCount { get; set; }
}
