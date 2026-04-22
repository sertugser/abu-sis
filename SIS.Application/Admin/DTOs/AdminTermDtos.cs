namespace SIS.Application.Admin.DTOs;

public record AdminTermDto(
    int Id,
    string Name,
    string ShortName,
    string EnglishName,
    string ShortEnglishName,
    string? OtherName,
    int Year,
    string TermType,              // "Fall" | "Spring" | ... 9 değer
    string TermTypeLabel,         // "Güz" | "Bahar" | ...
    string Status,                // "Inactive" | "Active" | "Cancelled" — computed
    string StatusLabel,           // "Pasif" | "Aktif" | "İptal"
    bool IsCancelled,             // stored — admin'in tek manuel aksiyonu
    string YoksisStatus,          // "Inactive" | "Active"
    DateTime RegistrationStart, DateTime RegistrationEnd,
    DateTime AddDropStart, DateTime AddDropEnd,
    DateTime TermStart, DateTime TermEnd,
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
    bool IsActive,                // computed: GetStatus(now) == Active
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
    DateTime RegistrationStart, DateTime RegistrationEnd,
    DateTime AddDropStart, DateTime AddDropEnd,
    DateTime TermStart, DateTime TermEnd,
    DateTime WithdrawalDeadline,
    DateTime GradeEntryStart, DateTime GradeEntryEnd
);

public record UpdateTermDto(
    string Name,
    string ShortName,
    string EnglishName,
    string ShortEnglishName,
    string? OtherName,
    string TermType,
    string YoksisStatus,
    DateTime RegistrationStart, DateTime RegistrationEnd,
    DateTime AddDropStart, DateTime AddDropEnd,
    DateTime TermStart, DateTime TermEnd,
    DateTime WithdrawalDeadline,
    DateTime GradeEntryStart, DateTime GradeEntryEnd
);

public record UpdateTermPhaseOverrideDto(
    string Phase,      // "Registration" | "AddDrop" | "Withdrawal" | "GradeEntry"
    bool? Value         // null = takvim konuşsun; true/false = admin override
);
