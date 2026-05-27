namespace SIS.Application.CourseRegistration.DTOs;

/// <summary>"+"a basınca açılan şube seçim penceresinin bir satırı (Proliz "Dersler" popup).</summary>
public sealed record SectionPickerRowDto(
    int SectionId, int SectionNumber, string Code, string Name, string Schedule,
    bool IsCompulsory, int TheoryHours, int LabHours, int NationalCredit, int Ects,
    string InstructorName, int ClassYear,
    string? OpeningReasonName,
    string TakeTypeLabel,
    int Capacity, int EnrolledCount, int AvailableSeats, bool QuotaVisible,
    string? GroupCode,
    bool IsEnrollmentSuspended, bool IsActive, bool IsEligible, string? IneligibleReason,
    string? PrerequisiteNote);

public sealed record SectionPickerResultDto(
    IReadOnlyList<SectionPickerRowDto> Items,
    int TotalCount, int Page, int PageSize);
