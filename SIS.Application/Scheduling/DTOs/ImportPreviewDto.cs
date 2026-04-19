namespace SIS.Application.Scheduling.DTOs;

public class ImportPreviewDto
{
    public int TotalRows { get; set; }
    public List<ImportRowDto> Rows { get; set; } = new();
}

public class ImportRowDto
{
    public int RowNumber { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string SectionCode { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string Day { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? SectionId { get; set; }
    public int? ClassroomId { get; set; }
    public int? TimeSlotId { get; set; }
}

public class ImportResultDto
{
    public int AppliedCount { get; set; }
    public int FailedCount { get; set; }
    public List<ImportFailureDto> Failures { get; set; } = new();
}

public class ImportFailureDto
{
    public int RowNumber { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
