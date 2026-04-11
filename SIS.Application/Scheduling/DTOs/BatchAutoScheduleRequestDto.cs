namespace SIS.Application.Scheduling.DTOs;

public class BatchAutoScheduleRequestDto
{
    public int? TermId { get; set; }
    public int? DepartmentId { get; set; }
    public int? FacultyId { get; set; }
    public bool ForceRegenerate { get; set; }
}
