using SIS.Domain.Enums;

namespace SIS.Application.Admin.DTOs;

/// <summary>
/// Read model for a student's placement/entrance info.
/// <see cref="EntranceType"/> is the enum name (e.g. "Osym") or null;
/// <see cref="EntranceTypeLabel"/> is its Turkish display label (null when type is null).
/// <see cref="Id"/> is null when the student exists but has no placement row yet.
/// </summary>
public sealed record StudentPlacementDto(
    int? Id,
    string? EntranceType,
    string? EntranceTypeLabel,
    string? ScoreType,
    decimal? Score,
    int? RankOrder,
    string? QuotaType,
    int? PlacementYear,
    int? PreferenceOrder,
    string? PlacedProgramName,
    string? Note);

/// <summary>
/// Write model for upserting a student's placement info.
/// <see cref="EntranceType"/> must be a valid <see cref="StudentEntranceType"/> enum name or null.
/// </summary>
public sealed record UpsertStudentPlacementDto(
    string? EntranceType,
    string? ScoreType,
    decimal? Score,
    int? RankOrder,
    string? QuotaType,
    int? PlacementYear,
    int? PreferenceOrder,
    string? PlacedProgramName,
    string? Note);

/// <summary>
/// Turkish display labels for <see cref="StudentEntranceType"/> values.
/// </summary>
public static class EntranceTypeLabels
{
    public static string Of(StudentEntranceType type) => type switch
    {
        StudentEntranceType.Osym => "ÖSYM/YKS",
        StudentEntranceType.Dgs => "DGS",
        StudentEntranceType.HorizontalTransfer => "Yatay Geçiş",
        StudentEntranceType.InternationalStudent => "Yabancı Uyruklu",
        StudentEntranceType.SpecialTalent => "Özel Yetenek",
        StudentEntranceType.Other => "Diğer",
        _ => type.ToString(),
    };
}
