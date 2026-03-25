namespace SIS.Application.FacultyPlanning.DTOs;

public class FacultyPlanningOverviewDto
{
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = string.Empty;
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int TotalSections { get; set; }
    public int ScheduledSections { get; set; }
    public int UnscheduledSections { get; set; }
    public int ConflictCount { get; set; }
    public Dictionary<string, int> ConflictCountByType { get; set; } = new();
    public List<DepartmentProgressDto> DepartmentProgress { get; set; } = new();
    public List<PendingApprovalWarningDto> PendingApprovalWarnings { get; set; } = new();
    public List<RecentAssignmentDto> RecentAssignments { get; set; } = new();
}

public class DepartmentProgressDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public int TotalSections { get; set; }
    public int ScheduledSections { get; set; }
    public int PendingApprovals { get; set; }
}

public class PendingApprovalWarningDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int PendingCount { get; set; }
}

public class RecentAssignmentDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string? ClassroomName { get; set; }
    public string? ScheduleSummary { get; set; }
    public string AssignedByUserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}
