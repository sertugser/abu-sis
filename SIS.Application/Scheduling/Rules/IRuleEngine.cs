using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.Scheduling.Rules;

/// <summary>
/// Plan 11 — Runtime hard-rule orchestrator. Predicate seti üzerinde Plan 10
/// disabled/exempt/category mantığını uygular, sonuçları <see cref="SchedulingConflictDto"/>
/// formatına map'ler (geriye uyumlu UI).
///
/// Faz 4'te SchedulingService.ValidateAsync / GetConflictsAsync feature flag açıkken
/// ConstraintEvaluationService yerine bu engine'a delegasyon yapar.
/// </summary>
public interface IRuleEngine
{
    /// <summary>
    /// Tek bir candidate'ı snapshot'a karşı değerlendirir (manuel atama, ValidateAsync için).
    /// </summary>
    Task<IReadOnlyList<SchedulingConflictDto>> EvaluateCandidateAsync(
        RuntimeCandidate candidate,
        RuntimeSnapshot snapshot,
        SchedulingConstraintConfig config,
        ConstraintState? state,
        CancellationToken ct);

    /// <summary>
    /// Snapshot'taki tüm section'ları taramada her birinde mevcut ihlalleri toplar
    /// (GetConflictsAsync / dashboard için). Bidirectional dedup yapılır.
    /// </summary>
    Task<IReadOnlyList<SchedulingConflictDto>> EvaluateAllExistingAsync(
        RuntimeSnapshot snapshot,
        SchedulingConstraintConfig config,
        ConstraintState? state,
        CancellationToken ct);
}
