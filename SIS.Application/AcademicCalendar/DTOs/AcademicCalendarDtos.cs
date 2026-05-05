namespace SIS.Application.AcademicCalendar.DTOs;

public record AcademicCalendarDto(
    int Id,
    int TermId,
    string TermName,
    DateTime TermStart,
    DateTime TermEnd,
    DateTime RegistrationStart, DateTime RegistrationEnd,
    DateTime AddDropStart, DateTime AddDropEnd,
    DateTime WithdrawalDeadline,
    DateTime GradeEntryStart, DateTime GradeEntryEnd,
    bool? RegistrationOverride,
    bool? AddDropOverride,
    bool? WithdrawalOverride,
    bool? GradeEntryOverride,
    bool IsRegistrationOpen,
    bool IsAddDropOpen,
    bool IsWithdrawalOpen,
    bool IsGradeEntryOpen,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record UpsertAcademicCalendarDto(
    DateTime RegistrationStart, DateTime RegistrationEnd,
    DateTime AddDropStart, DateTime AddDropEnd,
    DateTime WithdrawalDeadline,
    DateTime GradeEntryStart, DateTime GradeEntryEnd
);

public record UpdateCalendarPhaseOverrideDto(
    string Phase,   // "Registration" | "AddDrop" | "Withdrawal" | "GradeEntry"
    bool? Value     // null = takvim konuşsun; true/false = admin override
);

public record TermCalendarEventDto(
    int Id,
    string EventType,
    string Title,
    DateTime StartDate,
    DateTime? EndDate,
    string? Note,
    bool IsPublic,
    bool IsGenerated,
    bool BlocksAcademicEvents,
    string? ChainCode,
    string? TemplateItemKey
);

public record UpdateTermCalendarEventDto(
    DateTime StartDate,
    DateTime? EndDate,
    bool? PropagateChanges = true
);

public record StudentAcademicCalendarDto(
    int TermId,
    string TermName,
    string TermTypeLabel,
    int Year,
    bool IsCancelled,
    DateTime TermStart,
    DateTime TermEnd,
    DateTime? RegistrationStart, DateTime? RegistrationEnd,
    DateTime? AddDropStart, DateTime? AddDropEnd,
    DateTime? WithdrawalDeadline,
    DateTime? GradeEntryStart, DateTime? GradeEntryEnd,
    bool IsRegistrationOpen,
    bool IsAddDropOpen,
    bool IsWithdrawalOpen,
    bool IsGradeEntryOpen,
    IReadOnlyList<TermCalendarEventDto> Events
);

public record AcademicCalendarTermSummaryDto(
    int TermId,
    string TermName,
    int Year,
    string TermTypeLabel,
    bool IsActive,
    bool IsCancelled,
    bool HasCalendar,
    DateTime TermStart,
    DateTime TermEnd
);
