namespace SIS.Application.Scheduling.DTOs;

public sealed record DepartmentScheduleTermDto(
    int TermId,
    string Label,
    bool IsDefault);
