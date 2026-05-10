namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Schedule bloku tipi. Plan 9 sonrası v1 engine silindi; bu type'lar evaluator'lar +
/// SectionSnapshot yapıları tarafından hâlâ kullanılıyor (constraint preview, simulation).
/// </summary>
public enum BlockType { Theory, Lab }

/// <summary>
/// Bir section'ın belirli bir gün/saat aralığındaki yerleşimini ifade eder.
/// ClassroomId nullable: online dersler için null (classroom ataması yok, sadece zaman).
/// </summary>
public sealed record BlockAssignment(
    BlockType Type,
    int? ClassroomId,
    DayOfWeek Day,
    TimeOnly StartTime,
    TimeOnly EndTime);

/// <summary>
/// Aday üretici tarafından dönen tek bir kombinasyon — theory bloğu + lab bloklarının listesi.
/// Plan 9 sonrası v1 CandidateGenerator silindi; type ConflictStatistics + benzeri analiz
/// helper'lar tarafından evaluator-based explain akışında kullanılıyor.
/// </summary>
public sealed class BlockCombination
{
    public BlockAssignment? TheoryBlock { get; init; }
    public List<BlockAssignment> LabBlocks { get; init; } = new();
}
