namespace SIS.Application.Admin.DTOs;

public record AdminTermDto(
    int Id,
    string Name,
    string ShortName,
    string EnglishName,
    int Year,
    string Semester,
    string SemesterLabel,
    string TermType,
    string TermTypeLabel,

    string Status,          // "Draft" | "Preparing" | "Ready" | "Closed" | "Archived"
    string StatusLabel,
    string[] AllowedTransitions,

    // Tarih alanları
    DateTime RegistrationStart,
    DateTime RegistrationEnd,
    DateTime AddDropStart,
    DateTime AddDropEnd,
    DateTime TermStart,
    DateTime TermEnd,
    DateTime WithdrawalDeadline,
    DateTime GradeEntryStart,
    DateTime GradeEntryEnd,

    // Override alanları
    bool? RegistrationOverride,
    bool? AddDropOverride,
    bool? WithdrawalOverride,
    bool? GradeEntryOverride,

    // Computed phase flags (server-side, now'a göre)
    bool IsRegistrationOpen,
    bool IsAddDropOpen,
    bool IsWithdrawalOpen,
    bool IsGradeEntryOpen,
    bool IsActive,

    // Takvim event bilgileri
    int CalendarEventCount,
    int GeneratedCalendarEventCount,
    int? GeneratedCalendarDraftId,
    bool HasGeneratedCalendar
);

public record TransitionTermStatusDto(string TargetStatus);

public record CreateTermDto(
    string Name,
    string ShortName,
    string EnglishName,
    int Year,
    string Semester,
    string TermType,
    DateTime RegistrationStart,
    DateTime RegistrationEnd,
    DateTime AddDropStart,
    DateTime AddDropEnd,
    DateTime TermStart,
    DateTime TermEnd,
    DateTime WithdrawalDeadline,
    DateTime GradeEntryStart,
    DateTime GradeEntryEnd
);

public record UpdateTermDto(
    string Name,
    string ShortName,
    string EnglishName,
    string TermType,
    DateTime RegistrationStart,
    DateTime RegistrationEnd,
    DateTime AddDropStart,
    DateTime AddDropEnd,
    DateTime TermStart,
    DateTime TermEnd,
    DateTime WithdrawalDeadline,
    DateTime GradeEntryStart,
    DateTime GradeEntryEnd
);

public record UpdateTermPhaseOverrideDto(
    string Phase,   // "Registration" | "AddDrop" | "Withdrawal" | "GradeEntry"
    bool? Value     // null | true | false
);
