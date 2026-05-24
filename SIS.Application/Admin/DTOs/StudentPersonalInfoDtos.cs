namespace SIS.Application.Admin.DTOs;

/// <summary>
/// Read model for a student's personal info. Enum fields are exposed as strings
/// (global JsonStringEnumConverter). All fields may be null when the row is empty
/// or not yet created.
/// </summary>
public record StudentPersonalInfoDto(
    int StudentId,
    // Contact
    string? MobilePhone,
    string? Phone,
    string? SecondaryEmail,
    string? AddressProvince,
    string? AddressDistrict,
    string? AddressLine,
    string? PostalCode,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelation,
    // Demographic
    DateTime? BirthDate,
    string? BirthPlace,
    string? Gender,
    string? Nationality,
    string? BloodType,
    string? MaritalStatus,
    string? PassportNo,
    // Military
    string? MilitaryStatus,
    DateTime? MilitaryDeferralUntil,
    string? MilitaryOffice,
    // Photo
    string? PhotoExtension
);

/// <summary>
/// Write model for upserting a student's personal info. Enum fields are accepted
/// as strings and parsed by the service (invalid → InvalidOperationException).
/// </summary>
public record UpdateStudentPersonalInfoDto(
    // Contact
    string? MobilePhone,
    string? Phone,
    string? SecondaryEmail,
    string? AddressProvince,
    string? AddressDistrict,
    string? AddressLine,
    string? PostalCode,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelation,
    // Demographic
    DateTime? BirthDate,
    string? BirthPlace,
    string? Gender,
    string? Nationality,
    string? BloodType,
    string? MaritalStatus,
    string? PassportNo,
    // Military
    string? MilitaryStatus,
    DateTime? MilitaryDeferralUntil,
    string? MilitaryOffice,
    // Photo
    string? PhotoExtension
);
