namespace SIS.Application.Admin.DTOs;

public record OsymRegistrationCalendarDto(
    int Id,
    int Year,
    string Type,           // enum.ToString()
    string TypeLabel,      // "YKS Yerleştirme" vb.
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateOsymRegistrationCalendarDto(
    int Year,
    string Type,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string? Description,
    bool IsActive
);

public record UpdateOsymRegistrationCalendarDto(
    int Year,
    string Type,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string? Description,
    bool IsActive
);
