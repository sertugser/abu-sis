using SIS.Application.Scheduling.Constraints;

namespace SIS.Application.Scheduling.Rules;

/// <summary>
/// Plan 11 — IRulePredicate.Evaluate'ın aldığı runtime context. Mevcut
/// <c>SchedulingConstraintContext</c>'in yeni isim altındaki karşılığı.
/// Candidate + filtered snapshot + effective config + Plan 10 state (disabled/exempt/category).
/// </summary>
public sealed class RuleContext
{
    public required RuntimeCandidate Candidate { get; init; }
    public required RuntimeSnapshot Snapshot { get; init; }

    /// <summary>
    /// SchedulingConstraintConfig korunur — Plan 10 JSON merge (MergeParameterOverride) mevcut
    /// reflection-based davranışı tekrar yazmak yerine RuleEngine bu config'i hazırlayıp predicate'a
    /// pas geçer. Predicate'lar config field'ları okur.
    /// </summary>
    public required SchedulingConstraintConfig Config { get; init; }

    /// <summary>Plan 10 disabled/exempt/category state. null ise default (her şey aktif).</summary>
    public ConstraintState? State { get; init; }
}
