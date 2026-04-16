namespace SIS.Application.Scheduling.DTOs;

public sealed record InstructorPreferenceDto(
    int Id,
    int InstructorId,
    string InstructorName,
    string PreferenceType,
    string PreferenceLevel,
    int? DayOfWeek,
    TimeOnly? TimeSlotStart,
    TimeOnly? TimeSlotEnd,
    int? ClassroomId,
    string? ClassroomName,
    string? BuildingName,
    int? MaxHours,
    string? Reason,
    int? TermId,
    bool IsApproved,
    DateTime CreatedAt);

public sealed record CreateInstructorPreferenceRequest(
    string PreferenceType,
    string PreferenceLevel,
    int? DayOfWeek,
    string? TimeSlotStart,
    string? TimeSlotEnd,
    int? ClassroomId,
    string? BuildingName,
    int? MaxHours,
    string? Reason,
    int? TermId);
