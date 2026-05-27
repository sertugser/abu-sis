using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

/// <summary>
/// 1-1 extension of <see cref="Student"/> holding contact, demographic and
/// military-service details. All fields are optional — a student may have no
/// personal-info row, or a row with mostly null values, until staff fill it in.
/// </summary>
public class StudentPersonalInfo
{
    public int Id { get; set; }
    public int StudentId { get; set; }

    // ── Contact ─────────────────────────────────────────────
    public string? MobilePhone { get; set; }
    public string? Phone { get; set; }
    public string? SecondaryEmail { get; set; }
    public string? AddressProvince { get; set; }
    public string? AddressDistrict { get; set; }
    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelation { get; set; }

    // ── Demographic ─────────────────────────────────────────
    public DateTime? BirthDate { get; set; }
    public string? BirthPlace { get; set; }
    public Gender? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? BloodType { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public string? PassportNo { get; set; }

    // ── Military service ────────────────────────────────────
    public MilitaryServiceStatus? MilitaryStatus { get; set; }
    public DateTime? MilitaryDeferralUntil { get; set; }
    public string? MilitaryOffice { get; set; }

    // ── Photo ───────────────────────────────────────────────
    public string? PhotoExtension { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
