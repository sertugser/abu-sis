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
    DateTime RegistrationStart,
    DateTime RegistrationEnd,
    DateTime AddDropStart,
    DateTime AddDropEnd,
    DateTime TermStart,
    DateTime TermEnd,
    DateTime WithdrawalDeadline,
    bool IsRegistrationOpen,
    bool IsAddDropOpen
);

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

public record TermCalendarEventDto(
    int Id,
    int TermId,
    string EventType,       // enum name, e.g. "MidtermExam"
    string EventTypeLabel,  // Turkish label, e.g. "Ara Sınav"
    string Title,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsPublic,
    string? Note
);

public record CreateCalendarEventDto(
    string EventType,
    string Title,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsPublic,
    string? Note
);

public record UpdateCalendarEventDto(
    string EventType,
    string Title,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsPublic,
    string? Note
);
