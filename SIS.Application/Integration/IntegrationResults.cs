namespace SIS.Application.Integration;

/// <summary>
/// Result of an NVİ (Nüfus ve Vatandaşlık İşleri) identity verification query.
/// <see cref="IsMock"/> is true when the data is fabricated test data rather than
/// a real NVİ response. <see cref="Mode"/> echoes the configured integration mode.
/// </summary>
public sealed record IdentityVerificationResult(
    bool IsMock,
    bool Matched,
    string? FullName,
    DateTime? BirthDate,
    string? BirthPlace,
    string? AddressProvince,
    string? AddressDistrict,
    string Mode,
    DateTime QueriedAt,
    string? Message);

/// <summary>
/// Result of a YÖKSİS student reporting status query or submission.
/// <see cref="IsMock"/> is true when the data is fabricated test data.
/// </summary>
public sealed record YoksisStatusResult(
    bool IsMock,
    string Status,
    string? YoksisRecordNo,
    DateTime QueriedAt,
    string Mode,
    string? Message);

/// <summary>
/// Result of an ÖSYM placement query. This is a read-only preview; persisting it
/// (e.g. via the placement upsert endpoint) is a separate, deliberate action.
/// </summary>
public sealed record OsymPlacementResult(
    bool IsMock,
    string EntranceType,
    string? ScoreType,
    decimal? Score,
    int? RankOrder,
    string? QuotaType,
    int? PlacementYear,
    string? PlacedProgramName,
    DateTime QueriedAt,
    string Mode,
    string? Message);
