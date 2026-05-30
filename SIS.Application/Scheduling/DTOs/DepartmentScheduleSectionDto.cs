using SIS.Domain.Enums;

namespace SIS.Application.Scheduling.DTOs;

public sealed record DepartmentScheduleSectionDto(
    int SectionId,
    string SectionNumber,
    string CourseCode,
    string CourseName,
    string InstructorDisplay,
    List<DepartmentScheduleBlockDto> Blocks);

public sealed record DepartmentScheduleBlockDto(
    int Day,
    TimeOnly StartTime,
    TimeOnly EndTime,
    ScheduleBlockType BlockType,
    int? ClassroomId,
    string? ClassroomName,
    int? ClassroomCapacity);
