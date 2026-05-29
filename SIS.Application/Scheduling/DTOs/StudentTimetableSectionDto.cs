using SIS.Application.FacultyPlanning.DTOs;

namespace SIS.Application.Scheduling.DTOs;

public sealed record StudentTimetableSectionDto(
    int SectionId,
    string SectionNumber,
    string CourseCode,
    string CourseName,
    string InstructorDisplay,
    List<ScheduleBlockDto> Blocks);
