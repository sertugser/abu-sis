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

    /// <summary>
    /// Plan 12 — Tüm snapshot için score breakdown üretir: hard violation count + per-constraint
    /// soft penalty kalemleri. <c>GetScoreBreakdownAsync</c> + <c>SimulationService</c> bu method'u
    /// çağırır. V1 <c>ConstraintEvaluationService.EvaluateWithBreakdown</c> portu.
    /// </summary>
    Task<ScoreBreakdownDto> EvaluateWithBreakdownAsync(
        RuntimeSnapshot snapshot,
        SchedulingConstraintConfig config,
        ConstraintState? state,
        CancellationToken ct);

    /// <summary>
    /// Plan 12 — Tek candidate için soft penalty toplamı. V1
    /// <c>ConstraintEvaluationService.CalculatePenalty</c> portu. Auto-schedule optimizer
    /// (Plan 13 SA/LNS rollback) ve simulation suggestion path için tutulur.
    /// </summary>
    Task<double> CalculateCandidatePenaltyAsync(
        RuntimeCandidate candidate,
        RuntimeSnapshot snapshot,
        SchedulingConstraintConfig config,
        ConstraintState? state,
        CancellationToken ct);
}
