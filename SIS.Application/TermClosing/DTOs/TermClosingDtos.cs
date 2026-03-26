using SIS.Domain.Enums;

namespace SIS.Application.TermClosing.DTOs;

public class TermClosingStatusDto
{
    public int DepartmentId { get; set; }
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public List<TermClosingStepStatus> Steps { get; set; } = new();
}

public class TermClosingStepStatus
{
    public TermClosingStep Step { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByName { get; set; }
    public string? Notes { get; set; }
}

public class TermClosingStepResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class NewTermRequestDto
{
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public SemesterType Semester { get; set; }
    public DateTime RegistrationStart { get; set; }
    public DateTime RegistrationEnd { get; set; }
    public DateTime TermStart { get; set; }
    public DateTime TermEnd { get; set; }
}

public class NewTermResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? NewTermId { get; set; }
}
