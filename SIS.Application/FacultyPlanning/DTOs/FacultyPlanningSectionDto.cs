using SIS.Domain.Enums;

namespace SIS.Application.FacultyPlanning.DTOs;

public class FacultyPlanningSectionDto
{
    public int SectionId { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int? ClassroomId { get; set; }
    public string? ClassroomName { get; set; }
    public string? ClassroomType { get; set; }
    public int? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public string ScheduleSummary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public int SectionIndex { get; set; }
    public bool IsPinned { get; set; }
    public int SuggestedSemester { get; set; }
    public TeachingMethod TeachingMethod { get; set; } = TeachingMethod.FaceToFace;
}


public class FacultyPlanningSectionPageDto
{
    public List<FacultyPlanningSectionDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class FacultyPlanningTimeSlotDto
{
    public int Day { get; set; }
    public int SlotOrder { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public TimeSlotAllowedMode AllowedMode { get; set; }
}
