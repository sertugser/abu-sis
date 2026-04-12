using SIS.Domain.Enums;

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
    public int DraftOrPendingCount { get; set; }
    public int? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public bool IsActive { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
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

    // Forecast data
    public int PredictedDemand { get; set; }
    public int RetakeCount { get; set; }  // FF+NA+U
    public int RecommendedSectionCount { get; set; }
    public int RecommendedCapacity { get; set; }

    // Cross-department talep
    public List<CrossDepartmentDemandDto> CrossDepartmentDemand { get; set; } = new();

    // Önceki dönem verisi
    public int? PreviousTermEnrollment { get; set; }

    // Approval entegrasyonu
    public SectionApprovalStatus? ApprovalStatus { get; set; }
    public string ApprovalWorkflowStatus { get; set; } = "Pending";
    public int? ApprovedSectionCount { get; set; }
    public int OpenedSectionCount { get; set; }
    public int RemainingApprovedSectionCount { get; set; }
    public bool CanOpen { get; set; }
    public string? OpeningBlockedReason { get; set; }
}

public class CrossDepartmentDemandDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public string ProgramName { get; set; } = "";
    public int EstimatedStudentCount { get; set; }
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
    /// <summary>
    /// Dersin hangi bölümlere açık olacağı. Null/boş ise müfredattaki tüm bölümlere otomatik açılır.
    /// </summary>
    public List<int>? AccessibleDepartmentIds { get; set; }
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

// ── Bulk Assign ──

public class BulkAssignInstructorRequest
{
    public List<SectionInstructorAssignment> Assignments { get; set; } = [];
}

public class SectionInstructorAssignment
{
    public int SectionId { get; set; }
    public int InstructorId { get; set; }
}

public class BulkAssignInstructorResultDto
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = [];
}

public class InstructorWorkloadDto
{
    public int InstructorId { get; set; }
    public string FullName { get; set; } = "";
    public string Title { get; set; } = "";
    public int MaxWeeklyHours { get; set; }
    public int AssignedWeeklyHours { get; set; }
    public int RemainingCapacity { get; set; }
    public List<AssignedSectionDto> AssignedSections { get; set; } = new();
}

public class AssignedSectionDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int SectionNumber { get; set; }
    public int WeeklyHours { get; set; }
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
}

// ── Term Transfer ──

public record PreviousTermCourseDto(
    int CourseId, string CourseCode, string CourseName,
    int Ects, int SuggestedSemester, bool IsCompulsory,
    int SectionCount, int TotalCapacity,
    string? InstructorName, int? InstructorId,
    bool AlreadyOpened);

public class TransferFromTermRequest
{
    public int DepartmentId { get; set; }
    public int SourceTermId { get; set; }
    public int TargetTermId { get; set; }
    public List<int> CourseIds { get; set; } = [];
}

public class TransferFromTermResultDto
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalSectionsCreated { get; set; }
    public List<TransferCourseResultItem> Results { get; set; } = [];
}

public class TransferCourseResultItem
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int SectionsCreated { get; set; }
}
