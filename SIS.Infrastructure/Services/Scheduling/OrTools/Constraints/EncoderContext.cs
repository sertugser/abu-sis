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
}
