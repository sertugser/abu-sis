namespace SIS.Application.Scheduling.DTOs;

public sealed record DepartmentScheduleDto(
    int TermId,
    string TermName,
    int ProgramId,
    string ProgramName,
    string DepartmentName,
    int ClassYear,
    List<DepartmentScheduleSectionDto> Sections);
