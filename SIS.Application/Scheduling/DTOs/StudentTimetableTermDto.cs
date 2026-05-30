namespace SIS.Application.Scheduling.DTOs;

public sealed record StudentTimetableTermDto(
    int TermId,
    string Label,
    bool IsDefault);
