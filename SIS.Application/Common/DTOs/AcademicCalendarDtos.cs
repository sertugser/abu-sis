namespace SIS.Application.Common.DTOs;

public record PublicCalendarEventDto(
    int Id,
    string EventType,
    string EventTypeLabel,
    string Title,
    DateTime StartDate,
    DateTime? EndDate,
    string? Note
);

public record ActiveTermCalendarDto(
    int TermId,
    string TermName,
    string ShortName,
    string EnglishName,
    IReadOnlyList<PublicCalendarEventDto> Events
);
