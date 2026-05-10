using System.Collections.Generic;

namespace SIS.Application.Scheduling.Solver;

public sealed record SolverResult
{
    public required SolverStatus Status { get; init; }
    public required IReadOnlyList<Assignment> Assignments { get; init; }
    public required IReadOnlyList<Failure> Failures { get; init; }
    public required SolverDiagnostics Diagnostics { get; init; }
}
