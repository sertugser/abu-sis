namespace SIS.Application.Admin.DTOs;

public record AdminTermDto(
    int Id,
    string Name,
    string ShortName,
    string EnglishName,
    string ShortEnglishName,
    string? OtherName,
    int Year,
    string TermType,
    string TermTypeLabel,
    string Status,
    string StatusLabel,
    bool IsActivated,
    bool IsCancelled,
    string YoksisStatus,
    DateTime TermStart,
    DateTime TermEnd,
    DateTime? OverrideStart,
    DateTime? OverrideEnd,
    DateTime EffectiveStart,
    DateTime EffectiveEnd,
    bool IsActive,
    bool HasAcademicCalendar,
    int CalendarEventCount,
    int GeneratedCalendarEventCount,
    int? GeneratedCalendarDraftId,
    bool HasGeneratedCalendar
);

public record CreateTermDto(
    string Name,
    string ShortName,
    string EnglishName,
    string ShortEnglishName,
    string? OtherName,
    int Year,
    string TermType,
    string YoksisStatus,
    DateTime TermStart,
    DateTime TermEnd,
    DateTime? OverrideStart,
    DateTime? OverrideEnd
);

public record UpdateTermDto(
    string Name,
    string ShortName,
    string EnglishName,
    string ShortEnglishName,
    string? OtherName,
    string TermType,
    string YoksisStatus,
    DateTime TermStart,
    DateTime TermEnd,
    DateTime? OverrideStart,
    DateTime? OverrideEnd
);

// DELETED: UpdateTermPhaseOverrideDto — moved to AcademicCalendarDtos.cs
