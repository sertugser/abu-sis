using SIS.Domain.Enums;

namespace SIS.Application.Admin.DTOs;

/// <summary>
/// Read model for a single scholarship record held by a student.
/// <see cref="Type"/> is the enum name (e.g. "OsymScholarship");
/// <see cref="TypeLabel"/> is its Turkish display label.
/// </summary>
public sealed record ScholarshipDetailDto(
    int Id,
    string Type,
    string TypeLabel,
    decimal? RatePercent,
    string? Source,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Note);

/// <summary>
/// Write model for creating/updating a scholarship record.
/// <see cref="Type"/> must be a valid <see cref="ScholarshipType"/> enum name.
/// </summary>
public sealed record UpsertScholarshipDetailDto(
    string Type,
    decimal? RatePercent,
    string? Source,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Note);

/// <summary>
/// Turkish display labels for <see cref="ScholarshipType"/> values.
/// </summary>
public static class ScholarshipTypeLabels
{
    public static string Of(ScholarshipType type) => type switch
    {
        ScholarshipType.OsymScholarship => "ÖSYM Bursu",
        ScholarshipType.MeritScholarship => "Başarı Bursu",
        ScholarshipType.NeedBasedScholarship => "İhtiyaç Bursu",
        ScholarshipType.AthleticScholarship => "Spor Bursu",
        ScholarshipType.InstitutionalScholarship => "Kurum Bursu",
        ScholarshipType.Other => "Diğer",
        _ => type.ToString(),
    };
}
