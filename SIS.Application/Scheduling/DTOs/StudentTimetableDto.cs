namespace SIS.Application.Scheduling.DTOs;

public sealed record StudentTimetableDto(
    string StudentName,
    int TermId,
    string TermName,
    List<StudentTimetableSectionDto> Sections);
