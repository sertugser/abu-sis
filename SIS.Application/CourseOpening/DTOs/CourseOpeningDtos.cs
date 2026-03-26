namespace SIS.Application.CourseOpening.DTOs;

// ── Response DTOs ──

public class OpenedCourseSectionDto
{
    public int SectionId { get; set; }
    public int SectionNumber { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public int Credits { get; set; }
    public int Ects { get; set; }
    public int SuggestedSemester { get; set; }
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public int RegisteredCount { get; set; }
    public int? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public bool IsActive { get; set; }
}

public class OpenedCourseListDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public int TermId { get; set; }
    public string TermName { get; set; } = "";
    public int TotalSections { get; set; }
    public List<OpenedCourseSectionDto> Sections { get; set; } = [];
}

public class CurriculumCourseForOpeningDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public int Ects { get; set; }
    public int SuggestedSemester { get; set; }
    public bool IsCompulsory { get; set; }
    public bool AlreadyOpened { get; set; }
    public int ExistingSectionCount { get; set; }
}

public class InstructorOptionDto
{
    public int InstructorId { get; set; }
    public string FullName { get; set; } = "";
}

// ── Request DTOs ──

public class OpenCourseRequest
{
    public int CourseId { get; set; }
    public int SectionCount { get; set; } = 1;
    public int CapacityPerSection { get; set; } = 40;
    public int? InstructorId { get; set; }
}

public class BulkOpenCoursesRequest
{
    public List<OpenCourseRequest> Courses { get; set; } = [];
}

public class BulkOpenCoursesResultDto
{
    public int TotalCoursesOpened { get; set; }
    public int TotalSectionsCreated { get; set; }
    public List<string> Errors { get; set; } = [];
}

public class UpdateOpenedSectionRequest
{
    public int SectionId { get; set; }
    public int? InstructorId { get; set; }
    public int? Capacity { get; set; }
}
