using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;

public sealed class EncoderContext
{
    public required CpModel Model { get; init; }
    public required DecisionVariables Variables { get; init; }
    public required AssumptionRegistry Assumptions { get; init; }
    public required SolverRequest Request { get; init; }
    public required IReadOnlyDictionary<string, bool> ConstraintFlags { get; init; }
    public required IList<LinearExpr> Penalties { get; init; }

    /// <summary>
    /// Plan 7b — SystemConfig "Classroom.AmphiThreshold" (default 100). Capacity ≥ threshold
    /// olan section'lar yalnızca Amphitheater tipinde lecture room kullanabilir.
    /// </summary>
    public int AmphiThreshold { get; init; } = 100;
}
