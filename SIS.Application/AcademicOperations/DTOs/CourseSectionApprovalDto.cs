using SIS.Domain.Enums;

namespace SIS.Application.AcademicOperations.DTOs;

public class CourseSectionApprovalDto
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int RecommendedSectionCount { get; set; }
    public int? ApprovedSectionCount { get; set; }
    public int PredictedDemand { get; set; }
    public SectionApprovalStatus Status { get; set; }
    public string? Note { get; set; }
    public string? DecidedByUserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public class ApproveCourseSectionsRequest
{
    public int CourseId { get; set; }
    public int SectionCount { get; set; }
    public string? Note { get; set; }
}

public class RejectCourseSectionsRequest
{
    public int CourseId { get; set; }
    public string? Note { get; set; }
}

public class BulkApprovalRequest
{
    public List<ApproveCourseSectionsRequest> Approvals { get; set; } = new();
}

public class BulkApprovalResultDto
{
    public int ApprovedCount { get; set; }
    public int SectionsCreated { get; set; }
    public List<string> Errors { get; set; } = new();
}
