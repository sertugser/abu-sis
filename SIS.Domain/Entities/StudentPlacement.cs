using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

/// <summary>
/// 1-1 extension of <see cref="Student"/> holding ÖSYM/DGS entrance and
/// placement information (score type, score, rank, quota, placement year,
/// preference order, placed program). All fields are optional — no row exists
/// until staff fill it in.
/// </summary>
public class StudentPlacement
{
    public int Id { get; set; }
    public int StudentId { get; set; }

    public StudentEntranceType? EntranceType { get; set; }
    public string? ScoreType { get; set; }
    public decimal? Score { get; set; }
    public int? RankOrder { get; set; }
    public string? QuotaType { get; set; }
    public int? PlacementYear { get; set; }
    public int? PreferenceOrder { get; set; }
    public string? PlacedProgramName { get; set; }
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
