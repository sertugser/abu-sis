using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.FacultyPlanning.DTOs;

public class AutoScheduleRequest
{
    public int FacultyId { get; set; }
    public int TermId { get; set; }
    public int? DepartmentId { get; set; }
    public List<int>? SectionIds { get; set; }
}

public class AutoSchedulePreviewDto
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<AutoSchedulePreviewItemDto> Assignments { get; set; } = new();
    public List<AutoScheduleFailureDto> Failures { get; set; } = new();
}

public class AutoSchedulePreviewItemDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public int ClassroomId { get; set; }
    public string ClassroomName { get; set; } = string.Empty;
    public List<ScheduleSlotDto> ScheduleSlots { get; set; } = new();
    public string ScheduleSummary { get; set; } = string.Empty;
    public int? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public decimal FitScore { get; set; }
}

public class AutoScheduleFailureDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class ApplyScheduleRequest
{
    public List<ApplyScheduleItemRequest> Assignments { get; set; } = new();
}

public class ApplyScheduleItemRequest
{
    public int SectionId { get; set; }
    public int ClassroomId { get; set; }
    public List<ScheduleSlotDto> ScheduleSlots { get; set; } = new();
    public int? InstructorId { get; set; }
}

public class ApplyScheduleResultDto
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
