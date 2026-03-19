namespace SIS.Application.Common.DTOs;

public class CurriculumStatusDto
{
    public string StudentName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public string CurriculumVersion { get; set; } = string.Empty;
    public int TotalCredits { get; set; }
    public int TotalEcts { get; set; }
    public decimal Cgpa { get; set; }
    public int PassedCredits { get; set; }
    public int PassedEcts { get; set; }
    public List<CurriculumSemesterDto> Semesters { get; set; } = new();
}

public class CurriculumSemesterDto
{
    public int SemesterNumber { get; set; }
    public string Label { get; set; } = string.Empty;
    public List<CurriculumCourseStatusDto> Courses { get; set; } = new();
    public int TotalCredits { get; set; }
    public int TotalEcts { get; set; }
    public int CourseCount { get; set; }
    public int CompulsoryCount { get; set; }
    public int ElectiveCount { get; set; }
}

public class CurriculumCourseStatusDto
{
    // Curriculum (left side)
    public int CourseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsCompulsory { get; set; }
    public int Credits { get; set; }
    public int Ects { get; set; }

    // Taken (right side)
    public string? TakenTermLabel { get; set; }
    public string? TakenCourseCode { get; set; }
    public string? TakenCourseName { get; set; }
    public bool? TakenIsCompulsory { get; set; }
    public int? TakenCredits { get; set; }
    public int? TakenEcts { get; set; }
    public string? Grade { get; set; }
    public string? GradeStatus { get; set; }

    // Prerequisites
    public List<string> Prerequisites { get; set; } = new();
    public bool PrerequisitesMet { get; set; }
}
