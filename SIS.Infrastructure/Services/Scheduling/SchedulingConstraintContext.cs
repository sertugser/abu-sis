using SIS.Application.Scheduling.Constraints;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Plan 11 Faz 2 — V1 evaluator path için kalan context wrapper. SchedulingConstraintConfig
/// SIS.Application'a taşındı (Plan 11 Rules namespace ile aynı katmanda erişilebilir olmalı).
/// Faz 5'te V1 evaluator silinince bu context tipi de silinir; yeni RulePredicate path'i
/// RuleContext kullanır.
/// </summary>
public sealed class SchedulingConstraintContext : IConstraintContext
{
    public required SectionSnapshot Candidate { get; init; }
    public required TermSnapshot Snapshot { get; init; }
    public required SchedulingConstraintConfig Config { get; init; }
}
