namespace SIS.Application.Admin.DTOs;

public record AdminTermDto(
    int Id,
    string Name,
    string ShortName,
    string EnglishName,
    int Year,
    string Semester,        // "Fall" | "Spring" | "Summer"
    string SemesterLabel,   // "Güz" | "Bahar" | "Yaz"
    string TermType,        // "Regular" | "Summer" | "Intensive" | "Exemption"
    string TermTypeLabel,   // "Normal" | "Yaz" | "Tekders" | "Muafiyet"
    bool IsActive,
    string Status,          // TermStatus enum name
    string StatusLabel,     // Turkish label
    string[] AllowedTransitions, // Next valid states
    DateTime RegistrationStart,
    DateTime RegistrationEnd,
    DateTime AddDropStart,
    DateTime AddDropEnd,
    DateTime TermStart,
    DateTime TermEnd,
    DateTime WithdrawalDeadline,
    bool IsRegistrationOpen,
    bool IsAddDropOpen,
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
    DateTime WithdrawalDeadline
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
    DateTime WithdrawalDeadline
);
