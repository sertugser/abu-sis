using System.Collections.Generic;

namespace SIS.Application.Scheduling.Solver;

public sealed record SolverDiagnostics
{
    public required string EngineVersion { get; init; }
    public required double ElapsedSeconds { get; init; }
    public required long Branches { get; init; }
    public required long Conflicts { get; init; }
    public required double ObjectiveValue { get; init; }
    public string? SearchStrategy { get; init; }
    public bool? UnsatCoreEnabled { get; init; }
    public IReadOnlyList<UnsatCoreEntry> UnsatCore { get; init; } = [];
}

public sealed record UnsatCoreEntry(
    string ConstraintCode,
    string HumanReason,
    int? PrimarySectionId,
    int? SecondarySectionId);
