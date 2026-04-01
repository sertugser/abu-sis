namespace SIS.Application.Scheduling.DTOs;

public class FacultyScheduleApprovalStatusDto
{
    public bool IsApproved { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Note { get; set; }
    public int FacultyId { get; set; }
    public int TermId { get; set; }
}

public class ApproveFacultyScheduleRequestDto
{
    public int TermId { get; set; }
    public string? Note { get; set; }
}
