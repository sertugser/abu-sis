namespace SIS.Application.Scheduling.Rules;

/// <summary>
/// Plan 12 — Soft predicate çıktısı. Hard <see cref="RuleViolation"/>'dan farklı olarak
/// numerik penalty + örnek mesaj listesi tutar (breakdown panel için).
///
/// <para><c>RawPenalty</c>: V1 evaluator <c>SoftPenalty</c> field'ının portu — birebir aynı
/// değer üretmeli (Plan 12 Faz 0.1 pinning baseline koruması).</para>
/// </summary>
public sealed record SoftRuleResult(
    double RawPenalty,
    int ViolationCount,
    int AffectedSectionCount,
    IReadOnlyList<string> SampleMessages)
{
    /// <summary>Penalty olmadığı durum için fabrika.</summary>
    public static SoftRuleResult Zero { get; } = new(0, 0, 0, Array.Empty<string>());
}
